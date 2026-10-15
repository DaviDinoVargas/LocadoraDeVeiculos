"""OWASP ZAP (DAST) contra a Web API de TESTE: varredura ATIVA guiada pelo contrato OpenAPI da própria API, autenticada como uma empresa.

Uso:  python zap_dast.py [--sem-build] [--minutos N]      (ou  ./rodar.sh zap)

O que faz: sobe a Web API de teste (banco LocadoraDeVeiculos_Seguranca, limites altos para o scanner não ser barrado pelo rate limit), cria uma
empresa com dados (cliente, condutor, carros, aluguel, cupom...), sobe o ZAP em modo daemon, importa o Swagger da API, injeta o token JWT da
empresa em todas as requisições do scanner (renovado a cada 8 min: o token vale 15) e roda a varredura ativa. No fim confere duas coisas:
(1) o que o ZAP achou, por risco; (2) que a varredura foi de verdade (quantos endpoints e quantas respostas 2xx) e não um passeio de 404.

Só contra 127.0.0.1. Nada é gravado no repositório: ZAP, Java e o perfil do ZAP ficam fora (ZAP_HOME, JRE em A:\\ferramentas-seg) e os resultados em
SEGURANCA_RESULTADOS. A chave da API do ZAP é gerada na hora e nunca impressa.
"""
import argparse
import datetime
import json
import os
import secrets
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

AQUI = Path(__file__).resolve().parent
sys.path.insert(0, str(AQUI))

from lib import ambiente  # noqa: E402
from lib.contexto import Contexto  # noqa: E402
from lib.http import ip_unico, requisitar  # noqa: E402
from lib.registro import Registro  # noqa: E402

PORTA_ZAP = 8090
ZAP_HOME = Path(os.environ.get('ZAP_HOME') or r'A:\ferramentas-seg\zap')
JRE_RAIZ = Path(os.environ.get('JRE_HOME_ZAP') or r'A:\ferramentas-seg\jre')

# Alertas que o ZAP emite para qualquer site mas que não se aplicam a esta API, com o MOTIVO. Só entra aqui o que foi conferido na resposta real.
ACEITOS = {
    'Storable and Cacheable Content': 'a API envia Cache-Control: no-store em todas as respostas (conferido em D01/M16); o alerta vem de respostas de erro do Kestrel sem corpo',
    'Non-Storable Content': 'informativo: confirma que as respostas NÃO são guardadas em cache, que é o desejado numa API autenticada',
    'Authentication Request Identified': 'informativo: o ZAP reconheceu o endpoint de login',
    'Session Management Response Identified': 'informativo: o ZAP reconheceu o cookie de sessão',
    'User Agent Fuzzer': 'informativo: a API responde igual a user-agents diferentes, sem conteúdo por navegador',
    'Private IP Disclosure': 'a trilha de auditoria mostra ao dono da empresa o IP de origem de cada evento (recurso, não vazamento); os 10.x.x.x são os IPs fictícios que os próprios ataques mandam em X-Forwarded-For',
}

# Falsos positivos de risco alto/médio, um a um: (alerta, caminho, parâmetro) -> por que é falso e qual verificação do ataque manual (C13/C14) prova.
FALSOS_POSITIVOS = {
    ('SQL Injection', '/api/grupos-automovel', 'nome'): 'o ZAP compara respostas de "John Doe AND 1=1 --" e "AND 1=2 --" num POST de cadastro; as duas viram nomes de grupo '
                                                         'diferentes e a diferença é só o estado (nome novo × repetido). Prova: C13 (os três registros nascem iguais e voltam literais)',
    ('Path Traversal', '/api/grupos-automovel', 'nome'): 'confiança baixa e sem evidência: o ZAP trocou o nome do grupo por "grupos-automovel" e nada de arquivo voltou; a API nunca '
                                                          'usa esse campo como caminho. Prova: C14 (travessia vira texto, sem arquivo do servidor)',
}


PROVAS_DOS_FALSOS_POSITIVOS_OK = False   # só vira True depois de C13 e C14 passarem NESTA execução, contra a mesma API que o ZAP atacou


def eh_falso_positivo(alerta: dict) -> bool:
    caminho = urllib.parse.urlsplit(alerta['url']).path
    return PROVAS_DOS_FALSOS_POSITIVOS_OK and (alerta['alert'], caminho, alerta.get('param', '')) in FALSOS_POSITIVOS


def java_exe() -> Path:
    candidatos = sorted(JRE_RAIZ.glob('jdk-*-jre/bin/java.exe'))
    if not candidatos:
        raise SystemExit(f'Java não encontrado em {JRE_RAIZ}. Veja o README (seção ZAP).')
    return candidatos[-1]


class ZapApi:
    def __init__(self, porta: int, chave: str):
        self.base, self.chave = f'http://127.0.0.1:{porta}', chave

    def chamar(self, caminho: str, **params):
        params['apikey'] = self.chave
        url = f'{self.base}/JSON/{caminho}/?{urllib.parse.urlencode(params)}'
        ultimo = None
        for tentativa in range(5):   # o ZAP às vezes devolve 500 enquanto grava o banco interno (checkpoint do HSQLDB): espera e repete
            try:
                with urllib.request.urlopen(url, timeout=120) as r:
                    return json.loads(r.read().decode('utf-8'))
            except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError) as ex:
                ultimo = ex
                time.sleep(5)
        raise ultimo


def subir_zap(pasta: Path, chave: str):
    if not (ZAP_HOME / 'zap-2.17.0.jar').exists():
        jars = list(ZAP_HOME.glob('zap-*.jar'))
        if not jars:
            raise SystemExit(f'ZAP não encontrado em {ZAP_HOME}. Veja o README (seção ZAP).')
    jar = sorted(ZAP_HOME.glob('zap-*.jar'))[-1]
    # o perfil do ZAP guarda o banco interno com as requisições da varredura (centenas de MB): fica no mesmo disco do ZAP, não no TEMP do sistema
    pasta_perfis = ZAP_HOME.parent / 'perfis'
    pasta_perfis.mkdir(exist_ok=True)
    perfil = Path(tempfile.mkdtemp(prefix='zap-perfil-', dir=pasta_perfis))
    log = open(pasta / 'zap.log', 'wb')
    env = ambiente.env_com_temp_no_disco_a()
    env['JAVA_HOME'] = str(java_exe().parents[1])
    proc = subprocess.Popen([str(java_exe()), '-Xmx1g', f'-Djava.io.tmpdir={perfil}', '-jar', str(jar), '-daemon', '-port', str(PORTA_ZAP), '-host', '127.0.0.1', '-dir', str(perfil),
                             '-silent', '-config', f'api.key={chave}', '-config', 'api.addrs.addr.name=127.0.0.1', '-config', 'api.addrs.addr.regex=false'],
                            cwd=ZAP_HOME, env=env, stdout=log, stderr=subprocess.STDOUT)
    return proc, perfil, log


def parar_zap(proc, perfil, log):
    if proc and proc.poll() is None:
        subprocess.run(['taskkill', '/PID', str(proc.pid), '/T', '/F'], capture_output=True)
        try:
            proc.wait(timeout=20)
        except subprocess.TimeoutExpired:
            pass
    log.close()
    import shutil
    shutil.rmtree(perfil, ignore_errors=True)


def montar_dados(base: str, ctx: Contexto):
    """Empresa com dados reais para o scanner ter o que atacar (ids de cliente, aluguel, cupom...)."""
    from ataques.h_negocio import _corpo_aluguel, _montar
    emp = ambiente.nova_empresa(base, 'zap')
    ctx.lembrar(emp.senha, emp.token)
    d = _montar(base, emp, carros=3)
    if d:
        requisitar('POST', f'{base}/api/alugueis', token=emp.token, ip=ip_unico(), json_corpo=_corpo_aluguel(d, dias_a_partir=10))
        requisitar('POST', f'{base}/api/taxas-servicos', token=emp.token, ip=ip_unico(), json_corpo={'nome': 'GPS ZAP', 'preco': 25, 'tipoCalculo': 'Fixo'})
        requisitar('POST', f'{base}/api/parceiros', token=emp.token, ip=ip_unico(), json_corpo={'nome': 'Parceiro ZAP', 'cnpj': ambiente.cnpj_valido(), 'categoria': 'Hotel'})
    return emp


def novo_token(emp) -> str:
    r = ambiente.login(emp.base, emp.email, emp.senha)
    token = (r.json() or {}).get('chave')
    if token:
        emp.token = token
    return emp.token


def resumo_md(info, reg, alertas, cobertura):
    ordem = ['High', 'Medium', 'Low', 'Informational']
    linhas = [f"# Resultado do OWASP ZAP — {info['data']}", '',
              f"- Commit: `{info['commit']}` · ZAP {info['zap']} · Duração da varredura ativa: {info['minutos']} min",
              f"- **Verificações aprovadas: {len(reg.aprovadas)} · reprovadas: {len(reg.reprovadas)}**",
              f"- Cobertura da varredura: {cobertura['urls']} URLs conhecidas, {cobertura['mensagens']} requisições enviadas, {cobertura['endpoints_2xx']} endpoints com resposta 2xx", '']
    por_risco = {r: [a for a in alertas if a['risk'] == r] for r in ordem}
    linhas += ['## Alertas por risco', '', '| Risco | Alertas distintos | Ocorrências |', '|---|---|---|']
    for r in ordem:
        nomes = {a['alert'] for a in por_risco[r]}
        linhas.append(f'| {r} | {len(nomes)} | {len(por_risco[r])} |')
    linhas.append('')
    for r in ordem:
        agrupados = {}
        for a in por_risco[r]:
            agrupados.setdefault(a['alert'], []).append(a)
        if not agrupados:
            continue
        linhas += [f'### {r}', '', '| Alerta | Ocorrências | Exemplo de URL | Situação |', '|---|---|---|---|']
        for nome, itens in sorted(agrupados.items()):
            if nome in ACEITOS:
                situacao = f'aceito: {ACEITOS[nome]}'
            elif all(eh_falso_positivo(i) for i in itens):
                situacao = 'falso positivo: ' + FALSOS_POSITIVOS[(nome, urllib.parse.urlsplit(itens[0]['url']).path, itens[0].get('param', ''))]
            else:
                situacao = '**ACHADO**' if r in ('High', 'Medium') else 'registrado'
            linhas.append(f"| {nome} | {len(itens)} | `{itens[0]['url'][:90]}` | {situacao} |")
        linhas.append('')
    linhas += ['## Verificações', '', '| ID | Resultado | Teste |', '|---|---|---|']
    for v in reg.itens:
        linhas.append(f"| {v.id} | {'INFO' if v.informativo else ('OK' if v.passou else '**FALHA**')} | {v.titulo} |")
    return '\n'.join(linhas) + '\n'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--sem-build', action='store_true')
    ap.add_argument('--minutos', type=int, default=30, help='tempo máximo da varredura ativa')
    args = ap.parse_args()

    pasta = ambiente.pasta_de_resultados() / f"{datetime.datetime.now():%Y-%m-%d_%H%M%S}_zap_{ambiente.commit_atual()}"
    pasta.mkdir(parents=True)
    print(f'Resultados em: {pasta}', flush=True)
    if not args.sem_build:
        ambiente.construir_api()

    reg = Registro()
    chave = secrets.token_urlsafe(24)
    api = ambiente.iniciar_api(pasta, ambiente.PORTA_API_CARGA, {'RATE_LIMIT_PER_MINUTE': '10000000', 'REGISTRATION_LIMIT_PER_HOUR': '100000'}, nome='api-zap')
    proc = perfil = log = None
    alertas, cobertura, minutos = [], {'urls': 0, 'mensagens': 0, 'endpoints_2xx': 0}, 0
    try:
        api.iniciar()
        base = f'http://127.0.0.1:{ambiente.PORTA_API_CARGA}'
        ctx = Contexto(base=base, reg=reg)
        ctx.lembrar(chave)
        emp = montar_dados(base, ctx)

        proc, perfil, log = subir_zap(pasta, chave)
        zap = ZapApi(PORTA_ZAP, chave)
        versao = None
        limite = time.time() + 240
        while time.time() < limite and versao is None:
            try:
                versao = zap.chamar('core/view/version')['version']
            except Exception:
                time.sleep(3)
        if not versao:
            raise SystemExit(f'O ZAP não respondeu; veja {pasta / "zap.log"}')
        print(f'ZAP {versao} pronto.', flush=True)
        # a API do ZAP responde antes de os complementos (replacer, openapi, ascan) terminarem de carregar: espera todos aparecerem
        limite = time.time() + 180
        while time.time() < limite:
            try:
                zap.chamar('replacer/view/rules')
                zap.chamar('ascan/view/scans')
                zap.chamar('openapi/view/version') if False else None
                break
            except Exception:
                time.sleep(3)

        zap.chamar('core/action/newSession', overwrite='true')
        zap.chamar('core/action/setMode', mode='attack')
        zap.chamar('ascan/action/setOptionMaxScanDurationInMins', Integer=str(args.minutos))
        zap.chamar('ascan/action/setOptionThreadPerHost', Integer='6')
        zap.chamar('ascan/action/setOptionMaxRuleDurationInMins', Integer='3')

        def definir_token():
            try:
                zap.chamar('replacer/action/removeRule', description='bearer')
            except Exception:
                pass
            zap.chamar('replacer/action/addRule', description='bearer', enabled='true', matchType='REQ_HEADER', matchRegex='false',
                       matchString='Authorization', replacement=f'Bearer {novo_token(emp)}', initiators='')
            ctx.lembrar(emp.token)
        definir_token()

        importado = zap.chamar('openapi/action/importUrl', url=f'{base}/swagger/v1/swagger.json', hostOverride=base.removeprefix('http://'))
        print('Contrato OpenAPI importado.', flush=True)

        scan = zap.chamar('ascan/action/scan', url=base, recurse='true', inScopeOnly='false')['scan']
        inicio, ultimo_token = time.time(), time.time()
        while True:
            estado = int(zap.chamar('ascan/view/status', scanId=scan)['status'])
            print(f'  varredura ativa: {estado}% ({int((time.time() - inicio) / 60)} min)', flush=True)
            if estado >= 100 or time.time() - inicio > (args.minutos + 3) * 60:
                break
            if time.time() - ultimo_token > 8 * 60:
                definir_token()
                ultimo_token = time.time()
            time.sleep(30)
        minutos = int((time.time() - inicio) / 60)
        if estado < 100:
            zap.chamar('ascan/action/stop', scanId=scan)

        alertas = zap.chamar('alert/view/alerts', baseurl=base, start='0', count='99999')['alerts']
        urls = zap.chamar('core/view/urls', baseurl=base)['urls']
        total = int(zap.chamar('core/view/numberOfMessages', baseurl=base)['numberOfMessages'])
        ok2xx = set()
        for inicio_pag in range(0, total, 1000):
            for m in zap.chamar('core/view/messages', baseurl=base, start=str(inicio_pag), count='1000')['messages']:
                if m['responseHeader'].startswith('HTTP/1.1 2'):
                    ok2xx.add((m['requestHeader'].split(' ', 2)[0], m['requestHeader'].split(' ', 2)[1].split('?')[0]))
        cobertura = {'urls': len(urls), 'mensagens': total, 'endpoints_2xx': len(ok2xx)}
        (pasta / 'zap-alertas.json').write_text(json.dumps(alertas, ensure_ascii=False, indent=1), encoding='utf-8')

        # --- verificações ----------------------------------------------------------------------------------------------------------------------
        # Os falsos positivos só valem se as provas manuais (C13/C14, módulo de entradas) passarem contra a MESMA API, agora.
        import importlib
        global PROVAS_DOS_FALSOS_POSITIVOS_OK
        importlib.import_module('ataques.c_entradas').executar(ctx)
        PROVAS_DOS_FALSOS_POSITIVOS_OK = all(v.passou for v in reg.itens if v.id in ('C13', 'C14')) and any(v.id == 'C13' for v in reg.itens)
        altos = sorted({a['alert'] for a in alertas if a['risk'] == 'High' and a['alert'] not in ACEITOS and not eh_falso_positivo(a)})
        medios = sorted({a['alert'] for a in alertas if a['risk'] == 'Medium' and a['alert'] not in ACEITOS and not eh_falso_positivo(a)})
        reg.checar('Z01', 'ZAP', 'A varredura ativa não achou nenhuma vulnerabilidade de risco ALTO', 'nenhuma', altos or 'nenhuma', not altos, 'alta', 'S12')
        reg.checar('Z02', 'ZAP', 'A varredura ativa não achou vulnerabilidade de risco MÉDIO (fora as justificadas)', 'nenhuma', medios or 'nenhuma', not medios, 'media', 'S12')
        reg.checar('Z03', 'ZAP', 'A varredura foi de verdade: conhece pelo menos 70 URLs (a API tem 99 operações em ~75 caminhos) e enviou milhares de requisições', '>= 70 URLs e >= 2000 requisições',
                   f"{cobertura['urls']} URLs; {cobertura['mensagens']} requisições", cobertura['urls'] >= 70 and cobertura['mensagens'] >= 2000, 'media')
        reg.checar('Z04', 'ZAP', 'Autenticada: o scanner obteve resposta 2xx em pelo menos 40 endpoints (não ficou só em 401/404)', '>= 40 endpoints com 2xx',
                   cobertura['endpoints_2xx'], cobertura['endpoints_2xx'] >= 40, 'media')
        reg.checar('Z05', 'ZAP', 'A varredura terminou dentro do tempo (não foi interrompida)', '100 %', f'{estado}% em {minutos} min', estado >= 100, 'baixa')
        baixos = sorted({a['alert'] for a in alertas if a['risk'] == 'Low' and a['alert'] not in ACEITOS})
        reg.info('Z06', 'ZAP', 'Alertas de risco baixo (registrados, não reprovam)', baixos or 'nenhum', '')
        info = {'data': datetime.datetime.now().isoformat(timespec='seconds'), 'commit': ambiente.commit_atual(), 'zap': versao, 'minutos': minutos}
        (pasta / 'resumo-zap.md').write_text(resumo_md(info, reg, alertas, cobertura), encoding='utf-8')
        (pasta / 'resultado.json').write_text(json.dumps({**reg.para_dict(), 'info': info, 'cobertura': cobertura}, ensure_ascii=False, indent=2), encoding='utf-8')
        print(f"\nAprovadas: {len(reg.aprovadas)} · Reprovadas: {len(reg.reprovadas)}\nResumo: {pasta / 'resumo-zap.md'}")
    finally:
        if proc:
            parar_zap(proc, perfil, log)
        api.parar()
    return 1 if reg.reprovadas else 0


if __name__ == '__main__':
    sys.exit(main())
