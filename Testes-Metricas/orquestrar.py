"""Orquestrador das métricas (Lighthouse e ponta a ponta). Uso:

    python orquestrar.py lighthouse|e2e|pacote|ml|carga|tudo|manter [--sem-build-api] [--sem-build-front]

Sobe, só para a medição, a Web API contra um banco SEPARADO (LocadoraDeVeiculos_Metricas, nunca o de uso), o build de
PRODUÇÃO do Angular numa porta própria (com proxy /api, como o nginx) e uma empresa de teste já com dados de exemplo.
Depois roda os scripts Node e derruba tudo. As credenciais da empresa de teste são geradas na hora, passadas aos scripts
por variável de ambiente e nunca impressas nem gravadas. Os resultados vão para FORA do repositório (METRICAS_RESULTADOS;
padrão: ../Metricas-Resultados), como os da segurança.

Reaproveita Testes-Seguranca/lib (banco de teste, subida da API, cadastro de conta); o nome do banco é trocado aqui.
"""
import argparse
import datetime
import json
import os
import platform
import subprocess
import sys
from pathlib import Path

AQUI = Path(__file__).resolve().parent
RAIZ = AQUI.parent
sys.path.insert(0, str(RAIZ / 'Testes-Seguranca'))   # pacote `lib` da bateria de segurança
sys.path.insert(0, str(AQUI))                         # pacote `apoio` deste diretório

from lib import ambiente  # noqa: E402

ambiente.BANCO_DE_TESTE = 'LocadoraDeVeiculos_Metricas'
from apoio import pacote as medidor_de_pacote  # noqa: E402
from apoio.semeadura import semear  # noqa: E402
from apoio.servidor_front import ServidorFront  # noqa: E402

PORTA_API = 5190
PORTA_FRONT = 4300
DIST_FRONT = RAIZ / 'Client' / 'dist' / 'client-app' / 'browser'


def pasta_de_resultados() -> Path:
    destino = Path(os.environ.get('METRICAS_RESULTADOS') or (RAIZ.parent / 'Metricas-Resultados')).resolve()
    if RAIZ in destino.parents or destino == RAIZ:
        raise SystemExit('METRICAS_RESULTADOS aponta para dentro do repositório: resultados de medição não vão para o git.')
    destino.mkdir(parents=True, exist_ok=True)
    return destino


def construir_front() -> None:
    print('Compilando o front (produção)...', flush=True)
    npm = 'npm.cmd' if platform.system() == 'Windows' else 'npm'
    r = subprocess.run([npm, 'run', 'build'], cwd=RAIZ / 'Client', capture_output=True, text=True)
    if r.returncode != 0 or not (DIST_FRONT / 'index.html').exists():
        raise SystemExit('Falha ao compilar o front.\n' + (r.stdout + r.stderr)[-1500:])


def rodar_node(script: str, env: dict, saida: Path) -> int:
    node = 'node.exe' if platform.system() == 'Windows' else 'node'
    return subprocess.run([node, script], cwd=AQUI, env=env).returncode


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument('modo', choices=['lighthouse', 'e2e', 'pacote', 'ml', 'carga', 'tudo', 'manter'])
    ap.add_argument('--sem-build-api', action='store_true')
    ap.add_argument('--sem-build-front', action='store_true')
    args = ap.parse_args()

    agora = datetime.datetime.now()
    commit = ambiente.commit_atual()
    pasta = pasta_de_resultados() / f'{agora:%Y-%m-%d_%H%M%S}_{commit}'
    pasta.mkdir(parents=True)
    print(f'Resultados em: {pasta}', flush=True)

    if args.modo == 'carga':
        # Tempos da API (p50/p95/p99 por cenário): é a bateria de carga da segurança, que já sobe a própria API de teste.
        print('Carga da API: delegando para Testes-Seguranca (resultados na pasta de resultados da segurança).', flush=True)
        return subprocess.run([sys.executable, '-X', 'utf8', str(RAIZ / 'Testes-Seguranca' / 'executar.py'), 'carga', '--sem-ml']
                              + (['--sem-build'] if args.sem_build_api else [])).returncode

    if args.modo == 'ml':
        # tempo de leitura de placa (OCR) sobre placas sintéticas; não precisa da API nem do front
        return subprocess.run([sys.executable, '-X', 'utf8', str(AQUI / 'ml' / 'tempo_leitura_placa.py'), str(pasta / 'ml')]).returncode

    if args.modo != 'pacote' and not args.sem_build_api:
        ambiente.construir_api()
    if not args.sem_build_front:
        construir_front()
    if not (DIST_FRONT / 'index.html').exists():
        raise SystemExit('Build do front não encontrado; rode sem --sem-build-front.')

    medida = medidor_de_pacote.medir(DIST_FRONT)
    (pasta / 'pacote.json').write_text(json.dumps(medida, ensure_ascii=False, indent=2), encoding='utf-8')
    (pasta / 'pacote.md').write_text(medidor_de_pacote.para_markdown(medida), encoding='utf-8')
    print(f"Pacote: inicial {medida['inicial']['bruto_kb']} KiB ({medida['inicial']['gzip_kb']} KiB comprimido); total {medida['total']['bruto_kb']} KiB", flush=True)
    if args.modo == 'pacote':
        return 0

    servicos, front, codigo = [], None, 0
    try:
        api = ambiente.iniciar_api(pasta, PORTA_API, {'RATE_LIMIT_PER_MINUTE': '10000000', 'REGISTRATION_LIMIT_PER_HOUR': '100000'}, nome='api')
        servicos.append(api)
        api.iniciar()
        base = f'http://127.0.0.1:{PORTA_API}'

        empresa = ambiente.nova_empresa(base, 'metricas')
        resumo = semear(base, empresa)
        print('Dados de exemplo criados:', json.dumps({k: v for k, v in resumo.items() if k != 'ids'}, ensure_ascii=False), flush=True)

        front = ServidorFront(DIST_FRONT, PORTA_FRONT, '127.0.0.1', PORTA_API)
        front.iniciar()
        print(f'Front (produção) em {front.url}', flush=True)

        env = dict(os.environ)
        env.update({'METRICAS_URL': front.url, 'METRICAS_API': base, 'METRICAS_EMAIL': empresa.email, 'METRICAS_SENHA': empresa.senha,
                    'METRICAS_SAIDA': str(pasta), 'METRICAS_COMMIT': commit,
                    'METRICAS_ALUGUEIS': json.dumps(resumo['ids'])})

        if args.modo == 'manter':
            print('\nAmbiente no ar. Ctrl+C encerra. (as credenciais não são exibidas)', flush=True)
            try:
                while True:
                    import time
                    time.sleep(60)
            except KeyboardInterrupt:
                pass
        if args.modo in ('lighthouse', 'tudo'):
            print('\n== Lighthouse ==', flush=True)
            codigo |= rodar_node('lighthouse/executar.mjs', env, pasta)
        if args.modo in ('e2e', 'tudo'):
            print('\n== Ponta a ponta ==', flush=True)
            codigo |= rodar_node('e2e/executar.mjs', env, pasta)
    finally:
        if front:
            front.parar()
        for s in servicos:
            s.parar()

    info = {'data': agora.isoformat(timespec='seconds'), 'commit': commit, 'maquina': platform.node(), 'modo': args.modo}
    (pasta / 'info.json').write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'\nPronto. Resultados: {pasta}')
    return codigo


if __name__ == '__main__':
    sys.exit(main())
