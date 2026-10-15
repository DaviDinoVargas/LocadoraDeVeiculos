"""Orquestrador dos testes de segurança. Uso:  python executar.py [ataques|carga|tudo] [--sem-ml] [--sem-build]

Sobe uma Web API de teste (banco LocadoraDeVeiculos_Seguranca) e um ML de teste, roda os módulos de ataque e/ou a carga,
confere se algum segredo gerado vazou para os logs e grava tudo FORA do repositório (SEGURANCA_RESULTADOS)."""
import argparse
import datetime
import importlib
import json
import platform
import secrets
import sys
from pathlib import Path

AQUI = Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI))

from lib import ambiente  # noqa: E402
from lib.contexto import Contexto  # noqa: E402
from lib.registro import Registro  # noqa: E402

MODULOS_ATAQUE = ['a_autenticacao', 'b_autorizacao', 'c_entradas', 'd_configuracao', 'e_ml', 'f_conta_e_privacidade', 'g_robustez', 'h_negocio',
                  'j_concorrencia', 'k_ml_fuzz', 'l_ssrf', 'm_sessoes_e_https']


def verificar_logs(ctx, pastas_logs):
    """Nenhuma senha nem token gerado pelos testes pode aparecer nos logs da API/ML (S22/S11)."""
    achados = []
    for log in pastas_logs:
        if not log.exists():
            continue
        texto = log.read_text(encoding='utf-8', errors='replace')
        for segredo in set(ctx.segredos_gerados):
            if segredo and segredo in texto:
                achados.append(f'{log.name}: um segredo gerado pelo teste (tamanho {len(segredo)}) apareceu no log')
    ctx.reg.checar('F01', 'Logs', 'Nenhuma senha ou token usado nos testes aparece nos logs da API/ML', 'nenhum vazamento',
                   achados or 'nenhum', not achados, 'alta', 'S22/S11')
    emails = [l for log in pastas_logs if log.exists() for l in log.read_text(encoding='utf-8', errors='replace').splitlines()
              if '@teste.local' in l and 'Tentativa de login' in l]
    sem_mascara = [l for l in emails if '***@' not in l]
    ctx.reg.checar('F02', 'Logs', 'E-mails em avisos de login aparecem mascarados', 'todos mascarados',
                   f'{len(sem_mascara)} sem máscara', not sem_mascara, 'media', 'S11')


def resumo_md(info, reg, carga):
    linhas = [f"# Resultado dos testes de segurança — {info['data']}", '',
              f"- Commit: `{info['commit']}` · Máquina: {info['maquina']} · Modo: {info['modo']}",
              f"- **Aprovadas: {len(reg.aprovadas)} · Reprovadas (achados): {len(reg.reprovadas)}** · Erros de script: {len(reg.erros_de_script)}", '']
    pendentes = [v for v in reg.itens if v.pendente]
    if pendentes:
        linhas += ['## Pendências conhecidas (não reprovam; dependem de produto ou de serviço externo)', '', '| ID | Ref. | Situação |', '|---|---|---|']
        for v in pendentes:
            linhas.append(f'| {v.id} | {v.ref} | {v.titulo}: {v.obtido[:140]} |')
        linhas.append('')
    if reg.reprovadas:
        linhas += ['## Achados (verificações reprovadas)', '', '| ID | Sev. | Ref. | Teste | Esperado | Obtido |', '|---|---|---|---|---|---|']
        ordem = {'alta': 0, 'media': 1, 'baixa': 2}
        for v in sorted(reg.reprovadas, key=lambda v: (ordem.get(v.severidade, 3), v.id)):
            linhas.append(f'| {v.id} | {v.severidade} | {v.ref} | {v.titulo} | {v.esperado} | {v.obtido[:90]} |')
        linhas.append('')
    linhas += ['## Todas as verificações', '', '| ID | Resultado | Categoria | Teste |', '|---|---|---|---|']
    for v in reg.itens:
        marca = 'INFO' if v.informativo else ('OK' if v.passou else '**FALHA**')
        linhas.append(f'| {v.id} | {marca} | {v.categoria} | {v.titulo} |')
    if carga:
        linhas += ['', '## Carga', '', '| Cenário | Conc. | req/s | p50 ms | p95 ms | p99 ms | 4xx | 429 | 5xx |', '|---|---|---|---|---|---|---|---|---|']
        for m in carga['medicoes']:
            linhas.append(f"| {m['cenario']} | {m['concorrencia']} | {m['rps']} | {m['p50_ms']} | {m['p95_ms']} | {m['p99_ms']} | {m['erros_4xx']} | {m['erros_429']} | {m['erros_5xx']} |")
        if carga.get('memoria_api_mb') is not None:
            linhas.append(f"\nMemória da API ao fim da carga: {carga['memoria_api_mb']} MB")
    if reg.erros_de_script:
        linhas += ['', '## Erros de script', ''] + [f'- {e}' for e in reg.erros_de_script]
    return '\n'.join(linhas) + '\n'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('modo', nargs='?', default='tudo', choices=['ataques', 'carga', 'tudo'])
    ap.add_argument('--sem-ml', action='store_true')
    ap.add_argument('--sem-build', action='store_true')
    ap.add_argument('--so', nargs='+', metavar='MODULO', help='roda só estes módulos de ataque (ex.: --so j_concorrencia), para iterar rápido')
    args = ap.parse_args()

    destino_base = ambiente.pasta_de_resultados()
    agora = datetime.datetime.now()
    commit = ambiente.commit_atual()
    pasta = destino_base / f"{agora:%Y-%m-%d_%H%M%S}_{commit}"
    pasta.mkdir(parents=True)
    print(f'Resultados em: {pasta}', flush=True)

    if not args.sem_build:
        ambiente.construir_api()

    reg = Registro()
    servicos, logs, carga = [], [], None
    token_ml = secrets.token_urlsafe(40)
    try:
        api = ambiente.iniciar_api(pasta, ambiente.PORTA_API, nome='api')
        servicos.append(api)
        api.iniciar()
        base = f'http://127.0.0.1:{ambiente.PORTA_API}'
        ctx = Contexto(base=base, reg=reg)
        ctx.lembrar(token_ml)
        ctx.pasta = pasta
        ctx.chave_jwt = ambiente._segredos().get('JWT_GENERATION_KEY')
        ctx.lembrar(ctx.chave_jwt)   # se a chave aparecer nos logs, F01 reprova

        ml = None
        if args.modo in ('ataques', 'tudo') and not args.sem_ml:
            try:
                # A empresa dona das câmeras existe antes do ML subir: o ML recebe o id dela em CAMERA_EMPRESA_ID.
                dona = ambiente.nova_empresa(base, 'dona')
                ctx.lembrar(dona.senha, dona.token)
                ctx.dona_das_cameras = dona
                ml = ambiente.iniciar_ml(pasta, token_ml, base, dona.empresa_id)
                servicos.append(ml)
                ml.iniciar()
                ctx.ml, ctx.token_ml = f'http://127.0.0.1:{ambiente.PORTA_ML}', token_ml
                ctx.ml_servico = ml
            except SystemExit as ex:
                print(f'ML indisponível: {ex}', flush=True)
                ctx.ml = None

        if args.modo in ('ataques', 'tudo'):
            for nome in (args.so or MODULOS_ATAQUE):
                print(f'\n== {nome} ==', flush=True)
                try:
                    importlib.import_module(f'ataques.{nome}').executar(ctx)
                except Exception as ex:  # um erro de script não pode esconder os demais resultados
                    reg.erro(nome, ex)

        if args.modo in ('carga', 'tudo'):
            print('\n== carga ==', flush=True)
            api_carga = ambiente.iniciar_api(pasta, ambiente.PORTA_API_CARGA, {'RATE_LIMIT_PER_MINUTE': '10000000', 'REGISTRATION_LIMIT_PER_HOUR': '100000'}, nome='api-carga')
            servicos.append(api_carga)
            api_carga.iniciar()
            ctx_carga = Contexto(base=f'http://127.0.0.1:{ambiente.PORTA_API_CARGA}', reg=reg)
            try:
                from carga import cenarios
                carga = cenarios.executar(ctx_carga, api_carga)
                ctx.segredos_gerados.extend(ctx_carga.segredos_gerados)
            except Exception as ex:
                reg.erro('carga', ex)
            api_carga.parar()

        print('\n== logs ==', flush=True)
        servicos.extend(ctx.servicos_extras)
        for s in servicos:
            s.parar()
        verificar_logs(ctx, [s.caminho_log for s in servicos])
    finally:
        for s in servicos:
            s.parar()

    info = {'data': agora.isoformat(timespec='seconds'), 'commit': commit, 'maquina': platform.node(), 'modo': args.modo,
            'python': platform.python_version()}
    saida = reg.para_dict()
    saida.update({'info': info, 'carga': carga})
    (pasta / 'resultado.json').write_text(json.dumps(saida, ensure_ascii=False, indent=2), encoding='utf-8')
    (pasta / 'resumo.md').write_text(resumo_md(info, reg, carga), encoding='utf-8')

    print(f"\nAprovadas: {len(reg.aprovadas)} · Reprovadas: {len(reg.reprovadas)} · Erros de script: {len(reg.erros_de_script)}")
    print(f'Resumo: {pasta / "resumo.md"}')
    return 1 if reg.erros_de_script else 0


if __name__ == '__main__':
    sys.exit(main())
