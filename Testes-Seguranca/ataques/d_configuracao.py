"""Configuração: cabeçalhos, CORS, anti-CSRF, vazamento de informação e verificações estáticas do código."""
import re

from lib import ambiente
from lib.http import ip_unico, requisitar

CAT = 'Configuração'
RAIZ = ambiente.RAIZ


def _ler(*partes) -> str:
    return (RAIZ.joinpath(*partes)).read_text(encoding='utf-8', errors='replace')


def executar(ctx):
    r, base = ctx.reg, ctx.base
    emp = ambiente.nova_empresa(base, 'cfg')
    ctx.lembrar(emp.senha, emp.token)

    # --- D01: cabeçalhos ------------------------------------------------------------------------
    resp = requisitar('GET', f'{base}/api/dashboard/resumo', token=emp.token, ip=ip_unico())
    h = resp.cabecalhos
    esperados = {
        'x-content-type-options': 'nosniff', 'x-frame-options': 'DENY', 'referrer-policy': 'no-referrer',
        'cache-control': 'no-store',
    }
    faltando = [k for k, v in esperados.items() if h.get(k) != v]
    if "frame-ancestors 'none'" not in h.get('content-security-policy', ''):
        faltando.append('content-security-policy')
    if 'camera=()' not in h.get('permissions-policy', ''):
        faltando.append('permissions-policy')
    r.checar('D01', CAT, 'Respostas da API trazem os cabeçalhos de segurança', 'todos presentes', faltando or 'todos', not faltando, 'media', 'S8')
    r.checar('D02', CAT, 'Sem X-Powered-By e sem versão no cabeçalho Server', 'sem X-Powered-By',
             f"x-powered-by={h.get('x-powered-by')} server={h.get('server')}", 'x-powered-by' not in h, 'baixa')

    # --- D03: CORS ------------------------------------------------------------------------------
    mau = requisitar('OPTIONS', f'{base}/api/clientes', ip=ip_unico(), cabecalhos={
        'Origin': 'http://evil.example', 'Access-Control-Request-Method': 'GET', 'Access-Control-Request-Headers': 'authorization'})
    r.checar('D03a', CAT, 'CORS: origem não listada não recebe Allow-Origin', 'sem Allow-Origin',
             mau.cabecalhos.get('access-control-allow-origin', '(ausente)'), 'access-control-allow-origin' not in mau.cabecalhos, 'alta')
    bom = requisitar('OPTIONS', f'{base}/api/clientes', ip=ip_unico(), cabecalhos={
        'Origin': 'http://localhost:4200', 'Access-Control-Request-Method': 'GET', 'Access-Control-Request-Headers': 'authorization'})
    r.checar('D03b', CAT, 'CORS: a origem do Angular é aceita e não vira "*"', 'origem exata',
             bom.cabecalhos.get('access-control-allow-origin'), bom.cabecalhos.get('access-control-allow-origin') == 'http://localhost:4200', 'media')

    # --- D04: anti-CSRF ----------------------------------------------------------------------------
    sem = requisitar('POST', f'{base}/api/auth/rotacionar', cookie=emp.cookie, ip=ip_unico())
    r.checar('D04', CAT, 'Rotacionar com o cookie, mas sem X-Locadora-Client, é recusado', '403', sem.status, sem.status == 403, 'alta', 'S15')

    # --- D05: erros não vazam detalhes ----------------------------------------------------------------
    inexistente = requisitar('GET', f'{base}/api/rota-que-nao-existe', token=emp.token, ip=ip_unico())
    guid_ruim = requisitar('GET', f'{base}/api/clientes/nao-eh-guid', token=emp.token, ip=ip_unico())
    vazou = [x.texto[:80] for x in (inexistente, guid_ruim) if any(t in x.texto for t in ('Exception', 'at Locadora', 'Microsoft.'))]
    r.checar('D05', CAT, 'Erros 404/400 não mostram stack trace nem tipos internos', 'sem vazamento', vazou or 'ok', not vazou, 'media')

    # --- D06: Swagger só em desenvolvimento (aqui o ambiente é Development: registrar) -------------------
    sw = requisitar('GET', f'{base}/swagger/index.html', ip=ip_unico())
    r.info('D06', CAT, 'Swagger em Development (esperado; fecha fora dele)', sw.status, 'S5')
    prog = _ler('Server', 'web-api', 'Program.cs')
    so_dev = re.search(r'IsDevelopment\(\)\s*\)\s*\{[^}]*UseSwagger', prog, re.S) is not None
    r.checar('D07', CAT, 'No código, o Swagger só é habilitado em Development', 'UseSwagger dentro de IsDevelopment()', so_dev, so_dev, 'media')

    # --- D10-D13: verificações estáticas do código (nada de executar) ----------------------------------
    settings = _ler('MachineLeaning', 'shared', 'settings.py')
    falha_aberta = re.search(r"ML_ENV'\s*,\s*'development'", settings) is not None
    r.checar('D10', CAT, 'ML: ambiente ausente conta como PRODUÇÃO (falha fechada)', 'padrão != development',
             'padrão = development (falha aberta)' if falha_aberta else 'falha fechada', not falha_aberta, 'media', 'S25')
    main = _ler('MachineLeaning', 'main.py')
    r.checar('D11', CAT, 'ML: escuta só em 127.0.0.1 por padrão', 'host 127.0.0.1',
             'host="0.0.0.0"' if 'host="0.0.0.0"' in main else 'ok', 'host="0.0.0.0"' not in main, 'media', 'S25')
    r.checar('D12', CAT, 'ML: reload automático só em desenvolvimento', 'reload condicional',
             'reload=True fixo' if 'reload=True' in main else 'ok', 'reload=True' not in main, 'baixa', 'S25')

    faces = RAIZ / 'MachineLeaning' / 'FaceAuth' / 'data' / 'faces.json'
    r.checar('D15', CAT, 'Não existe biometria em claro no disco (faces.json migrado para o banco cifrado e apagado)', 'arquivo ausente',
             f'existe ({faces.stat().st_size} bytes)' if faces.exists() else 'ausente', not faces.exists(), 'media', 'S1')

    csproj = _ler('Server', 'web-api', 'LocadoraDeVeiculos.WebApi.csproj')
    codigo = ''.join(p.read_text(encoding='utf-8', errors='ignore') for p in (RAIZ / 'Server' / 'web-api').rglob('*.cs')
                     if '\\obj\\' not in str(p) and '\\bin\\' not in str(p))
    mortos = [pacote for pacote, marca in (('Hangfire', 'Hangfire'), ('NewRelic', 'NewRelic')) if pacote in csproj and marca not in codigo]
    r.checar('D13', CAT, 'Sem pacotes referenciados e nunca usados (Hangfire, NewRelic)', 'nenhum', mortos or 'nenhum', not mortos, 'baixa', 'S30')

    # --- D16-D17: prova de vida (S3): carregamento seguro e padrão ligado ----------------------------------------
    vida = _ler('MachineLeaning', 'FaceAuth', 'antispoof', 'liveness.py')
    baixar = _ler('MachineLeaning', 'FaceAuth', 'antispoof', 'baixar_modelo.py')
    seguro = 'weights_only=True' in vida and 'sha256_do_arquivo(caminho) != hash_esperado' in vida
    fixo = re.search(r"COMMIT = '[0-9a-f]{40}'", baixar) is not None and "https://raw.githubusercontent.com/" in baixar and len(re.findall(r"[0-9a-f]{64}", vida)) >= 2
    r.checar('D16', CAT, 'Prova de vida: pesos conferidos por SHA-256 e carregados com weights_only=True', 'hash + weights_only no código',
             'ok' if seguro else 'faltando', seguro, 'alta', 'S3')
    r.checar('D16b', CAT, 'Prova de vida: download só de commit fixo, por HTTPS, com hash esperado no código', 'commit de 40 hex + 2 hashes',
             'ok' if fixo else 'faltando', fixo, 'media', 'S3')
    padrao_ligado = "ANTISPOOF_ENABLED', 'true'" in settings
    r.checar('D17', CAT, 'ML: a prova de vida vem LIGADA por padrão (falha fechada)', "padrão 'true'", 'ligada' if padrao_ligado else 'desligada',
             padrao_ligado, 'alta', 'S3')

    # --- D18: reconhecimento por embedding (S33): modelo conferido por hash e sem volta ao comparador por geometria ----------
    embedding = _ler('MachineLeaning', 'FaceAuth', 'embedding.py')
    roteador = _ler('MachineLeaning', 'FaceAuth', 'router.py')
    forte = ('SHA256_ESPERADO' in embedding and '_sha256(self.caminho) != self._sha256' in embedding and 'TAMANHO_ESPERADO' in embedding
             and 'comparar_faces' not in roteador and 'similaridade_cosseno' in roteador)
    r.checar('D18', CAT, 'Reconhecimento facial por embedding: modelo conferido por SHA-256/tamanho e o roteador não usa mais a geometria de landmarks',
             'hash no código e sem comparar_faces no router', 'ok' if forte else 'faltando', forte, 'alta', 'S33')

    seeder = RAIZ / 'Server' / 'LocadoraDeVeiculos.Infraestrutura.Orm' / 'Identify' / 'RoleSeeder.cs'
    r.checar('D14', CAT, 'Sem código morto que usa IgnoreQueryFilters (RoleSeeder)', 'arquivo removido', 'existe' if seeder.exists() else 'removido',
             not seeder.exists(), 'baixa', 'S30')
