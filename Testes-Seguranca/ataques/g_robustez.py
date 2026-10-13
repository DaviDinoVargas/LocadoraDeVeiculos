"""Robustez e abuso: JWT com a chave certa e claims inválidas, vazamento de campos sensíveis, fuzz, senhas gigantes, reset de senha
ponta a ponta (token cruzado, adulterado, corrida), força bruta do código 2FA, replay de TOTP, injeção em log, CORS e mass assignment."""
import re
import statistics
import time
import uuid
from concurrent.futures import ThreadPoolExecutor

from lib import ambiente
from lib.http import claims_do_jwt, ip_unico, jwt_forjado, requisitar

from ataques.f_conta_e_privacidade import totp

CAT = 'Robustez e abuso'

CAMPOS_SENSIVEIS = ['passwordhash', 'securitystamp', 'concurrencystamp', 'tokenhash', 'accesstokenversionid',
                    'authenticatorkey', 'recoverycode', 'refreshtoken', 'senhahash']
ROTAS_COM_ID = ['clientes', 'automoveis', 'grupos-automovel', 'alugueis', 'parceiros', 'funcionarios', 'cupons', 'condutores',
                'devolucoes', 'taxas-servicos', 'planos-cobranca']
IDS_ESTRANHOS = ['abc', '0', '-1', '00000000-0000-0000-0000-000000000000', '%00', '..%2f..%2fetc%2fpasswd', '%27%20OR%201%3D1--',
                 '%F0%9F%98%80', 'a' * 3000, '9' * 400, '%ZZ', '{id}', '%5C..%5C..%5Cwindows']


def _login(base, email, senha, codigo=None, ip=None, cabecalhos=None):
    corpo = {'email': email, 'senha': senha}
    if codigo is not None:
        corpo['codigoAutenticador'] = codigo
    return requisitar('POST', f'{base}/api/auth/autenticar', ip=ip or ip_unico(), json_corpo=corpo, cabecalhos=cabecalhos)


def _ler_log(ctx) -> str:
    try:
        return (ctx.pasta / 'api.log').read_text(encoding='utf-8', errors='replace')
    except OSError:
        return ''


def _links_de_reset(log: str):
    """[(base, email, token)] dos e-mails de desenvolvimento escritos no log da API (Development, sem SMTP)."""
    return [(m.group(1), m.group(2), m.group(3)) for m in
            re.finditer(r'(https?://[^\s/]+)/redefinir-senha#email=([^&\s]+)&token=([A-Za-z0-9_\-]+)', log)]


def _ativar_2fa(base, prefixo):
    conta = ambiente.nova_empresa(base, prefixo)
    chave = (requisitar('POST', f'{base}/api/auth/2fa/iniciar', token=conta.token, ip=ip_unico()).json() or {}).get('chave', '')
    ativar = requisitar('POST', f'{base}/api/auth/2fa/ativar', token=conta.token, ip=ip_unico(), json_corpo={'codigo': totp(chave)})
    codigos = (ativar.json() or {}).get('codigos', []) if ativar.ok else []
    return conta, chave, codigos


def executar(ctx):
    r, base = ctx.reg, ctx.base
    emp = ambiente.nova_empresa(base, 'rob')
    ctx.lembrar(emp.senha, emp.token)

    # --- G01: JWT com a chave CERTA mas claims inválidas (valida emissor, audiência, validade e versão da sessão) ---------
    if getattr(ctx, 'chave_jwt', None):
        chave = ctx.chave_jwt.encode()
        agora = int(time.time())
        alvo = f'{base}/api/dashboard/resumo'
        casos = {
            'controle (claims intactas)': ({}, True),
            'audiência errada': ({'aud': 'outro-publico'}, False),
            'emissor errado': ({'iss': 'OutroEmissor'}, False),
            'token vencido': ({'exp': agora - 3600}, False),
            'ainda não válido (nbf no futuro)': ({'nbf': agora + 3600}, False),
            'versão de sessão trocada (jti)': ({'jti': str(uuid.uuid4())}, False),
            'usuário inexistente (sub)': ({'sub': str(uuid.uuid4())}, False),
        }
        resultados = {}
        for nome, (claims, esperado_ok) in casos.items():
            token = jwt_forjado(emp.token, alterar_claims=claims, chave_hs256=chave)
            resultados[nome] = (requisitar('GET', alvo, token=token, ip=ip_unico()).status, esperado_ok)
        if resultados['controle (claims intactas)'][0] != 200:
            r.info('G01', CAT, 'JWT: controle com a chave de teste não passou; casos ignorados', resultados['controle (claims intactas)'][0], 'S17')
        else:
            erradas = {k: v[0] for k, v in resultados.items() if not v[1] and v[0] != 401}
            r.checar('G01', CAT, 'JWT assinado com a chave certa, mas com emissor/audiência/validade/sessão inválidos, é recusado',
                     '401 em todos os casos inválidos', erradas or '401', not erradas, 'alta', 'S17/S19')

    # --- G02: nenhum campo sensível do Identity/refresh token em respostas ---------------------------------------------------
    rotas = ['/api/clientes', '/api/funcionarios', '/api/auditoria?limite=50', '/api/auth/2fa', '/api/auth/sessao',
             '/api/dashboard/resumo', '/api/configuracoes', '/api/auth/rosto']
    corpos = [requisitar('GET', f'{base}{p}', token=emp.token, ip=ip_unico()).texto.lower() for p in rotas]
    corpos.append(_login(base, emp.email, emp.senha).texto.lower())
    vazados = sorted({campo for c in corpos for campo in CAMPOS_SENSIVEIS if campo in c})
    r.checar('G02', CAT, 'Respostas da API não expõem hash de senha, carimbo de segurança, hash de token nem chave do 2FA',
             'nenhum campo sensível', vazados or 'nenhum', not vazados, 'alta', 'S11')

    # --- G03: métodos HTTP inesperados -------------------------------------------------------------------------------------
    trace = requisitar('TRACE', f'{base}/api/clientes', token=emp.token, ip=ip_unico())
    r.checar('G03', CAT, 'TRACE não é atendido (sem eco da requisição)', '4xx/501 sem eco', trace.status,
             trace.status in (400, 404, 405, 501) and 'authorization' not in trace.texto.lower(), 'baixa', 'S8')

    # --- G04: fuzz de IDs e de parâmetros: nunca 5xx e a API continua de pé ---------------------------------------------------
    cinco_xx = []
    for rota in ROTAS_COM_ID:
        fuzz = ambiente.nova_empresa(base, 'fuzz')   # uma conta por rota: o limite global é de 300 pedidos/min por usuário
        ctx.lembrar(fuzz.senha, fuzz.token)
        for estranho in IDS_ESTRANHOS:
            for metodo in ('GET', 'DELETE'):
                resp = requisitar(metodo, f'{base}/api/{rota}/{estranho}', token=fuzz.token, ip=ip_unico(), timeout=15)
                if resp.status >= 500 or resp.erro:
                    cinco_xx.append((metodo, rota, estranho[:12], resp.status or resp.erro))
    consultas = ['limite=abc', 'limite=-1', 'limite=99999999999999999999', 'pagina=-1&tamanho=2147483647', 'filtro=%00', 'q=' + 'a' * 5000]
    for q in consultas:
        for rota in ('clientes', 'automoveis', 'auditoria'):
            resp = requisitar('GET', f'{base}/api/{rota}?{q}', token=emp.token, ip=ip_unico(), timeout=15)
            if resp.status >= 500 or resp.erro:
                cinco_xx.append(('GET', f'{rota}?{q[:14]}', '', resp.status or resp.erro))
    viva = requisitar('GET', f'{base}/api/auth/sessao', token=emp.token, ip=ip_unico())
    r.checar('G04', CAT, 'IDs e parâmetros estranhos (traversal, SQL, unicode, enormes, negativos) nunca causam 5xx nem derrubam a API',
             'nenhum 5xx e API viva', cinco_xx[:5] or ('viva' if viva.ok else viva.status), not cinco_xx and viva.ok, 'alta', 'S9')

    # --- G05: paginação abusiva ---------------------------------------------------------------------------------------------
    grande = requisitar('GET', f'{base}/api/auditoria?limite=100000000', token=emp.token, ip=ip_unico(), timeout=20)
    itens = ((grande.json() or {}).get('registros') or []) if grande.ok else []
    r.checar('G05', CAT, 'Pedir um limite gigante na auditoria não devolve tudo nem demora (teto)', 'resposta rápida, até 1000 itens',
             f'{grande.status} em {grande.ms:.0f} ms, {len(itens)} itens', grande.status in (200, 400) and len(itens) <= 1000 and grande.ms < 5000,
             'media', 'S9')

    # --- G06: senhas e e-mails gigantes ---------------------------------------------------------------------------------------
    enorme = 'Aa1' * 60_000
    lento_login = _login(base, emp.email, enorme)
    lento_reg = requisitar('POST', f'{base}/api/auth/registrar', ip=ip_unico(), json_corpo={
        'nomeCompleto': 'X', 'email': f'g06-{uuid.uuid4().hex[:6]}@teste.local', 'senha': enorme, 'confirmarSenha': enorme})
    longo = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), json_corpo={'email': 'a' * 20000 + '@x.com', 'senha': 'Abc12345'})
    r.checar('G06', CAT, 'Senha de 180 mil caracteres e e-mail de 20 mil são recusados com 4xx e sem demora', '4xx em menos de 3 s, nunca 5xx',
             [(x.status, round(x.ms)) for x in (lento_login, lento_reg, longo)],
             all(400 <= x.status < 500 and x.ms < 3000 for x in (lento_login, lento_reg, longo)), 'media', 'S9')

    # --- G07: variações do mesmo e-mail no cadastro respondem igual ao duplicado exato -------------------------------------------
    senha2 = ambiente.senha_forte()
    ctx.lembrar(senha2)

    def cadastrar(email):
        return requisitar('POST', f'{base}/api/auth/registrar', ip=ip_unico(), json_corpo={
            'nomeCompleto': 'Duplicado', 'email': email, 'senha': senha2, 'confirmarSenha': senha2})

    exato = cadastrar(emp.email)
    maiusculo = cadastrar(emp.email.upper())
    r.checar('G07', CAT, 'Cadastro com o e-mail em MAIÚSCULAS responde igual ao duplicado exato (sem criar segunda conta nem revelar)',
             'mesmo status e mesmo corpo', (exato.status, maiusculo.status), exato.status == maiusculo.status == 400 and exato.texto == maiusculo.texto,
             'media', 'S20')

    # --- G08: força bruta do código do 2FA (senha certa, códigos adivinhados) --------------------------------------------------------
    conta2, chave2, codigos2 = _ativar_2fa(base, 'bf2fa')
    ctx.lembrar(conta2.senha, chave2, *codigos2)
    if chave2 and len(codigos2) == 10:
        atacante = ip_unico()
        for i in range(5):
            _login(base, conta2.email, conta2.senha, f'{100000 + i * 7919:06d}', ip=atacante)
        barrado = _login(base, conta2.email, conta2.senha, totp(chave2), ip=atacante)
        dono = _login(base, conta2.email, conta2.senha, totp(chave2), ip=ip_unico())
        r.checar('G08', CAT, 'Depois de 5 códigos 2FA errados de um IP, esse IP é barrado até com o código certo; o dono, de outro IP, entra',
                 'atacante 4xx / dono 200', (barrado.status, dono.status), 400 <= barrado.status < 500 and dono.ok, 'alta', 'S18/S10')

    # --- G09/G10: replay do código do aplicativo e corrida com o código de recuperação ----------------------------------------------
    conta3, chave3, codigos3 = _ativar_2fa(base, 'replay')
    ctx.lembrar(conta3.senha, chave3, *codigos3)
    if chave3 and len(codigos3) == 10:
        codigo = totp(chave3)
        primeiro = _login(base, conta3.email, conta3.senha, codigo)
        repetido = _login(base, conta3.email, conta3.senha, codigo)
        r.checar('G09', CAT, 'O mesmo código do aplicativo não vale duas vezes (RFC 6238, seção 5.2)', '2ª tentativa recusada',
                 (primeiro.status, repetido.status), primeiro.ok and repetido.status == 400, 'media', 'S18')

        usar = codigos3[1]
        with ThreadPoolExecutor(max_workers=6) as pool:
            sucessos = [x.status for x in pool.map(lambda _: _login(base, conta3.email, conta3.senha, usar), range(6))].count(200)
        r.checar('G10', CAT, 'Corrida: o mesmo código de recuperação usado por 6 requisições simultâneas entra no máximo uma vez',
                 '<= 1 sucesso', sucessos, sucessos <= 1, 'alta', 'S18')

    # --- G11: injeção de linhas falsas no log ---------------------------------------------------------------------------------------
    marca = f'FORJADO{uuid.uuid4().hex[:8]}'
    _login(base, f'x@y.com\n{marca} Login OK usuario=admin', 'Qualquer@12345')
    _login(base, f'x@y.com\r\n{marca}2 Login OK', 'Qualquer@12345')
    time.sleep(0.5)
    forjadas = [l for l in _ler_log(ctx).splitlines() if l.strip().startswith(marca)]
    r.checar('G11', CAT, 'E-mail com quebra de linha não consegue forjar linhas no log da API', 'nenhuma linha começando com a marca',
             len(forjadas), not forjadas, 'media', 'S11')

    # --- G12: CORS com origens parecidas com a permitida ----------------------------------------------------------------------------
    permitidos = []
    for origem in ('null', 'http://localhost:4200.evil.com', 'http://evil-localhost:4200', 'https://localhost:4200', 'http://localhost:4200@evil.com'):
        resp = requisitar('OPTIONS', f'{base}/api/auth/autenticar', ip=ip_unico(), cabecalhos={
            'Origin': origem, 'Access-Control-Request-Method': 'POST'})
        permitido = resp.cabecalhos.get('access-control-allow-origin', '')
        if permitido and permitido != 'http://localhost:4200':
            permitidos.append((origem, permitido))
        elif permitido == origem and origem not in ('http://localhost:4200',):
            permitidos.append((origem, permitido))
    r.checar('G12', CAT, 'CORS só libera a origem exata configurada (null, sufixo, prefixo e usuário@ são recusados)', 'nenhuma origem extra',
             permitidos or 'nenhuma', not permitidos, 'media', 'S8')

    # --- G13: mass assignment no cadastro ---------------------------------------------------------------------------------------------
    email13 = f'mass-{uuid.uuid4().hex[:8]}@teste.local'
    alheio = emp.empresa_id
    reg13 = requisitar('POST', f'{base}/api/auth/registrar', ip=ip_unico(), json_corpo={
        'nomeCompleto': 'Mass', 'email': email13, 'senha': senha2, 'confirmarSenha': senha2,
        'cargo': 'Funcionario', 'role': 'Admin', 'roles': ['Admin'], 'empresaId': alheio, 'emailConfirmed': False,
        'isAdmin': True, 'lockoutEnabled': False, 'accessTokenVersionId': str(uuid.uuid4())})
    if reg13.ok:
        c = claims_do_jwt(reg13.json()['chave'])
        papel = c.get('http://schemas.microsoft.com/ws/2008/06/identity/claims/role') or c.get('role')
        proprio = c.get('EmpresaId') == c.get('sub')
        r.checar('G13', CAT, 'Campos extras no cadastro (cargo, empresaId, roles, isAdmin) são ignorados: a conta nasce Empresa do próprio tenant',
                 'papel Empresa e EmpresaId = próprio id', (papel, proprio), papel == 'Empresa' and proprio and c.get('EmpresaId') != alheio,
                 'alta', 'S15')
    else:
        r.checar('G13', CAT, 'Cadastro com campos extras não cria conta privilegiada', '4xx ou conta Empresa', reg13.status, reg13.status < 500, 'alta', 'S15')

    # --- G14: JSON hostil, tipos de conteúdo trocados e cabeçalhos gigantes ---------------------------------------------------------------
    aninhado = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), bruto=b'[' * 100_000)
    xml = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), bruto=b'<a/>', cabecalhos={'Content-Type': 'application/xml'})
    formulario = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), bruto=b'email=a&senha=b',
                            cabecalhos={'Content-Type': 'application/x-www-form-urlencoded'})
    cabecalho_grande = requisitar('GET', f'{base}/api/auth/sessao', ip=ip_unico(), cabecalhos={'X-Lixo': 'a' * 40_000})
    url_enorme = requisitar('GET', f'{base}/api/clientes?' + 'a=1&' * 8000, token=emp.token, ip=ip_unico())
    vivo = requisitar('GET', f'{base}/api/auth/sessao', token=emp.token, ip=ip_unico())
    resultados = {'json aninhado': aninhado.status, 'xml': xml.status, 'formulário': formulario.status,
                  'cabeçalho 40 KB': cabecalho_grande.status or cabecalho_grande.erro, 'URL enorme': url_enorme.status or url_enorme.erro}
    ruins = {k: v for k, v in resultados.items() if not (isinstance(v, int) and 400 <= v < 500) and k != 'URL enorme'}
    r.checar('G14', CAT, 'JSON com 100 mil colchetes, XML, formulário e cabeçalho de 40 KB são recusados com 4xx; a API segue de pé',
             '4xx em todos e API viva', ruins or ('viva' if vivo.ok else vivo.status), not ruins and vivo.ok and (url_enorme.status or 0) < 500,
             'media', 'S9')

    # --- G15/G16/G17: recuperação de senha ponta a ponta (lê o e-mail de desenvolvimento no log da API) --------------------------------------------
    a = ambiente.nova_empresa(base, 'resetA')
    b = ambiente.nova_empresa(base, 'resetB')
    ctx.lembrar(a.senha, b.senha)
    for conta in (a, b):
        requisitar('POST', f'{base}/api/auth/esqueci-senha', ip=ip_unico(), json_corpo={'email': conta.email})
    achados = {}
    for _ in range(30):
        time.sleep(0.5)
        for _, email, token in _links_de_reset(_ler_log(ctx)):
            achados[requests_unquote(email)] = token
        if a.email in achados and b.email in achados:
            break

    if a.email not in achados or b.email not in achados:
        r.info('G15', CAT, 'Reset de senha ponta a ponta ignorado: o e-mail de desenvolvimento não apareceu no log (SMTP configurado?)',
               sorted(achados), 'S18')
    else:
        nova = 'NovaSenha@' + uuid.uuid4().hex[:8]
        ctx.lembrar(nova)

        def redefinir(email, token, senha_nova, ip=None):
            return requisitar('POST', f'{base}/api/auth/redefinir-senha', ip=ip or ip_unico(), json_corpo={
                'email': email, 'token': token, 'novaSenha': senha_nova, 'confirmarNovaSenha': senha_nova})

        cruzado = redefinir(b.email, achados[a.email], nova)
        trocado = achados[a.email][:-2] + ('AA' if not achados[a.email].endswith('AA') else 'BB')
        adulterado = redefinir(a.email, trocado, nova)
        fraca = redefinir(a.email, achados[a.email], 'abc')
        b_intacta = _login(base, b.email, b.senha)
        r.checar('G15', CAT, 'Token de uma conta não redefine outra; token adulterado e senha fraca são recusados; a conta alvo segue intacta',
                 '400/400/400 e login da B 200', (cruzado.status, adulterado.status, fraca.status, b_intacta.status),
                 cruzado.status == adulterado.status == fraca.status == 400 and b_intacta.ok, 'alta', 'S18')

        # corrida: o mesmo token usado por 6 requisições ao mesmo tempo
        with ThreadPoolExecutor(max_workers=6) as pool:
            resultados = list(pool.map(lambda i: redefinir(a.email, achados[a.email], nova + str(i)).status, range(6)))
        r.checar('G16', CAT, 'Corrida: o mesmo token de redefinição usado 6 vezes ao mesmo tempo funciona no máximo uma vez',
                 '<= 1 sucesso', resultados, sum(1 for s in resultados if 200 <= s < 300) <= 1, 'alta', 'S18')

        antigo = requisitar('GET', f'{base}/api/dashboard/resumo', token=a.token, ip=ip_unico())
        velha = _login(base, a.email, a.senha)
        r.checar('G17', CAT, 'Depois da redefinição a senha antiga falha e o access token antigo deixa de valer', 'login 4xx e token 401',
                 (velha.status, antigo.status), 400 <= velha.status < 500 and antigo.status == 401, 'alta', 'S18/S27')

    # --- G18: o link do e-mail usa FRONTEND_URL, nunca o Host da requisição -----------------------------------------------------------------------
    h = ambiente.nova_empresa(base, 'host')
    ctx.lembrar(h.senha)
    requisitar('POST', f'{base}/api/auth/esqueci-senha', ip=ip_unico(), json_corpo={'email': h.email},
               cabecalhos={'Host': 'evil.example', 'X-Forwarded-Host': 'evil.example', 'Origin': 'http://evil.example'})
    base_do_link = None
    for _ in range(20):
        time.sleep(0.5)
        for url_base, email, _ in _links_de_reset(_ler_log(ctx)):
            if requests_unquote(email) == h.email:
                base_do_link = url_base
        if base_do_link:
            break
    if base_do_link:
        r.checar('G18', CAT, 'Cabeçalhos Host/X-Forwarded-Host/Origin forjados não mudam o endereço do link de redefinição', 'link sem evil.example',
                 base_do_link, 'evil' not in base_do_link, 'alta', 'S18')
    else:
        r.info('G18', CAT, 'Host header injection ignorado: o e-mail não apareceu no log (SMTP configurado?)', '', 'S18')

    # --- G19: tempo de resposta do "esqueci minha senha" igual para conta existente e inexistente ---------------------------------------------------------
    t_com, t_sem = [], []
    for _ in range(8):
        t_com.append(requisitar('POST', f'{base}/api/auth/esqueci-senha', ip=ip_unico(), json_corpo={'email': emp.email}).ms)
        t_sem.append(requisitar('POST', f'{base}/api/auth/esqueci-senha', ip=ip_unico(), json_corpo={'email': f'nao-{uuid.uuid4().hex[:6]}@teste.local'}).ms)
    mc, ms_ = statistics.median(t_com), statistics.median(t_sem)
    r.checar('G19', CAT, '"Esqueci minha senha" demora o mesmo para e-mail com e sem conta (sem enumeração por tempo)',
             'razão < 3x e diferença < 100 ms', f'com conta {mc:.0f} ms x sem conta {ms_:.0f} ms',
             max(mc, ms_) < 3 * max(min(mc, ms_), 1) or abs(mc - ms_) < 100, 'media', 'S20')


def requests_unquote(texto):
    from urllib.parse import unquote
    return unquote(texto)
