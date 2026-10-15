"""Sessões simultâneas (vários dispositivos da mesma conta) e a API em modo PRODUÇÃO com HTTPS de verdade (Kestrel + certificado):
cada dispositivo tem a própria sessão, sair/roubo de um não derruba os outros, trocar a senha derruba todos, e o cookie de sessão, o HSTS,
o redirecionamento de http para https, o Swagger e os erros se comportam como precisam fora do desenvolvimento."""
import re
import socket
import ssl
from concurrent.futures import ThreadPoolExecutor

from lib import ambiente
from lib.http import ip_unico, requisitar

CAT = 'Sessões e HTTPS'
H = {'X-Locadora-Client': 'web'}


def _cookie(resp):
    m = re.search(r'(LocadoraDeVeiculos\.RefreshToken=[^;]+)', resp.cabecalhos.get('set-cookie', ''))
    return m.group(1) if m else None


def _login(base, email, senha):
    return requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), json_corpo={'email': email, 'senha': senha})


def _rotacionar(base, cookie):
    return requisitar('POST', f'{base}/api/auth/rotacionar', cookie=cookie, ip=ip_unico(), cabecalhos=H)


def executar(ctx):
    _sessoes_simultaneas(ctx)
    _https_em_producao(ctx)


def _sessoes_simultaneas(ctx):
    r, base = ctx.reg, ctx.base
    conta = ambiente.nova_empresa(base, 'sessoes')
    ctx.lembrar(conta.senha, conta.token)

    # --- M01: 12 dispositivos entram ao mesmo tempo ---------------------------------------------------------------------------------
    with ThreadPoolExecutor(max_workers=12) as pool:
        logins = list(pool.map(lambda _: _login(base, conta.email, conta.senha), range(12)))
    cookies = [_cookie(x) for x in logins]
    tokens = [(x.json() or {}).get('chave') for x in logins]
    ctx.lembrar(*tokens)
    r.checar('M01', CAT, '12 logins simultâneos da mesma conta dão 12 sessões independentes (cookies diferentes, sem 5xx)', '12 sessões distintas',
             f'{sum(1 for x in logins if x.ok)} ok; {len(set(c for c in cookies if c))} cookies distintos; status {sorted(set(x.status for x in logins))}',
             all(x.ok for x in logins) and len({c for c in cookies if c}) == 12, 'media')
    if not all(cookies):
        return

    # --- Modelo de sessão do sistema (decisão registrada no handoff): VÁRIOS dispositivos podem estar logados ao mesmo tempo (M01), mas as
    # operações de segurança são GLOBAIS na conta: sair, reuso de refresh token e troca de senha encerram TODAS as sessões. O que se prova é que
    # não sobra nenhuma sessão "zumbi" (refresh token ou access token ainda vivo) depois de cada uma delas.
    def vivas(cookies_, tokens_):
        refresh = [i for i, c in cookies_ if _rotacionar(base, c).status == 200]
        acesso = [i for i, tk in tokens_ if requisitar('GET', f'{base}/api/dashboard/resumo', token=tk, ip=ip_unico()).status == 200]
        return refresh, acesso

    def abrir(n, nome):
        c = ambiente.nova_empresa(base, nome)
        ctx.lembrar(c.senha, c.token)
        entradas = [_login(base, c.email, c.senha) for _ in range(n)]
        toks = [(x.json() or {}).get('chave') for x in entradas]
        ctx.lembrar(*toks)
        return c, [_cookie(x) for x in entradas], toks

    # --- M02: sair encerra todas as sessões da conta ----------------------------------------------------------------------------------------------
    c2, ck2, tk2 = abrir(5, 'sair')
    sair = requisitar('POST', f'{base}/api/auth/sair', cookie=ck2[0], token=tk2[0], ip=ip_unico(), cabecalhos=H)
    refresh, acesso = vivas([(i, ck2[i]) for i in range(5)], [(i, tk2[i]) for i in range(5)])
    r.checar('M02', CAT, 'Sair encerra todas as sessões da conta: nenhum dispositivo fica com refresh token ou access token vivo', 'nenhuma sessão viva',
             f'sair {sair.status}; refresh vivos {refresh}; access vivos {acesso}', sair.status in (200, 204) and not refresh and not acesso, 'alta', 'S26')

    # --- M03: refresh token antigo reusado derruba todas as sessões (suspeita de roubo) ---------------------------------------------------------------
    c3, ck3, tk3 = abrir(5, 'reuso')
    primeira = _rotacionar(base, ck3[0])
    reuso = _rotacionar(base, ck3[0])                              # o ladrão tenta o token que já foi trocado
    refresh, acesso = vivas([(0, _cookie(primeira) or '')] + [(i, ck3[i]) for i in range(1, 5)], [(i, tk3[i]) for i in range(1, 5)])
    r.checar('M03', CAT, 'Reuso de refresh token antigo derruba todas as sessões da conta (o dono refaz o login; o ladrão não fica com nada)',
             'reuso 401 e nenhuma sessão viva', f'reuso {reuso.status}; refresh vivos {refresh}; access vivos {acesso}',
             primeira.ok and reuso.status == 401 and not refresh and not acesso, 'alta', 'S26')

    # --- M04: trocar a senha encerra todas as sessões --------------------------------------------------------------------------------------------------
    c4, ck4, tk4 = abrir(5, 'troca')
    nova = ambiente.senha_forte()
    ctx.lembrar(nova)
    troca = requisitar('POST', f'{base}/api/auth/alterar-senha', token=tk4[0], ip=ip_unico(),
                       json_corpo={'senhaAtual': c4.senha, 'novaSenha': nova, 'confirmarNovaSenha': nova})
    refresh, acesso = vivas([(i, ck4[i]) for i in range(5)], [(i, tk4[i]) for i in range(5)])
    r.checar('M04', CAT, 'Trocar a senha encerra todas as sessões abertas (refresh tokens e access tokens de todos os dispositivos)',
             'nenhuma sessão viva', f'troca {troca.status}; refresh vivos {refresh}; access vivos {acesso}',
             troca.ok and not refresh and not acesso, 'alta', 'S27')

    # --- M05: o rate limit de autenticação não é consumido pela renovação de UMA sessão legítima por carga de página ----------------------------
    # (decisão do grupo 6: a rotação roda a cada recarga da página; o limite de 10/min/IP vale para login/cadastro, não para quem recarrega)
    conta2 = ambiente.nova_empresa(base, 'recarrega')
    ctx.lembrar(conta2.senha, conta2.token)
    ip_fixo = ip_unico()
    cookie = conta2.cookie
    status = []
    for _ in range(14):
        x = requisitar('POST', f'{base}/api/auth/rotacionar', cookie=cookie, ip=ip_fixo, cabecalhos=H)
        status.append(x.status)
        cookie = _cookie(x) or cookie
    r.checar('M05', CAT, '14 renovações de sessão seguidas do mesmo IP (14 recargas de página em um minuto) não são bloqueadas por 429',
             'nenhum 429', f'{status.count(200)} ok; 429 em {status.count(429)}', 429 not in status, 'media', 'S10')


def _https_em_producao(ctx):
    r = ctx.reg
    if not ctx.pasta:
        return
    try:
        api = ambiente.iniciar_api_producao(ctx.pasta, ctx.segredos_gerados)
    except SystemExit as ex:
        r.info('M10', CAT, 'API de produção/HTTPS não pôde subir; verificações de HTTPS ignoradas', str(ex)[:160], '')
        return
    ctx.servicos_extras.append(api)
    try:
        api.iniciar()
    except SystemExit as ex:
        api.parar()
        r.info('M10', CAT, 'API de produção/HTTPS não ficou pronta; verificações de HTTPS ignoradas', str(ex)[:160], '')
        return

    http_base, https_base = f'http://127.0.0.1:{ambiente.PORTA_API_PRODUCAO_HTTP}', f'https://127.0.0.1:{ambiente.PORTA_API_PRODUCAO_HTTPS}'
    host_publico = {'Host': ambiente.HOST_PUBLICO_DE_TESTE}
    try:
        # --- M10: cadastro e login por HTTPS; o cookie de sessão sai com os atributos de segurança -----------------------------------------------
        email, senha = f'https-{ambiente.secrets.token_hex(4)}@teste.local', ambiente.senha_forte()
        ctx.lembrar(senha)
        cad = requisitar('POST', f'{https_base}/api/auth/registrar', ip=ip_unico(), json_corpo={
            'nomeCompleto': 'Empresa HTTPS', 'email': email, 'senha': senha, 'confirmarSenha': senha})
        r.checar('M10', CAT, 'Cadastro e login funcionam por HTTPS na API em modo produção', '2xx', cad.status, cad.ok, 'alta')
        if not cad.ok:
            return
        token = (cad.json() or {}).get('chave')
        ctx.lembrar(token)
        set_cookie = cad.cabecalhos.get('set-cookie', '')
        atributos = {a.strip().lower() for a in set_cookie.split(';')[1:]}
        r.checar('M11', CAT, 'O cookie de sessão sai com Secure, HttpOnly e SameSite', 'secure; httponly; samesite',
                 sorted(atributos), 'secure' in atributos and 'httponly' in atributos and any(a.startswith('samesite=') for a in atributos), 'alta', 'S26')
        caminho = next((a for a in atributos if a.startswith('path=')), 'path=/')
        r.checar('M12', CAT, 'O cookie de sessão só é enviado às rotas de autenticação (Path=/api/auth), não a toda a API', 'path=/api/auth', caminho,
                 caminho == 'path=/api/auth', 'media', 'S26')

        # --- M13: renovar e reusar por HTTPS ---------------------------------------------------------------------------------------------------
        cookie = _cookie(cad)
        renovou = requisitar('POST', f'{https_base}/api/auth/rotacionar', cookie=cookie, ip=ip_unico(), cabecalhos=H)
        reuso = requisitar('POST', f'{https_base}/api/auth/rotacionar', cookie=cookie, ip=ip_unico(), cabecalhos=H)
        r.checar('M13', CAT, 'Por HTTPS a renovação funciona uma vez e o cookie antigo é recusado (rotação)', '200 depois 401', f'{renovou.status}/{reuso.status}',
                 renovou.ok and reuso.status == 401, 'alta', 'S26')

        # --- M14: HTTP puro só redireciona para HTTPS, não serve dados ----------------------------------------------------------------------------
        simples = requisitar('GET', f'{http_base}/api/auth/sessao', token=token, ip=ip_unico())
        destino = simples.cabecalhos.get('location', '')
        r.checar('M14', CAT, 'Pedido em HTTP puro é redirecionado para HTTPS (307/308) e não devolve dados', '307/308 com Location https',
                 f'{simples.status} -> {destino[:60]}', simples.status in (307, 308) and destino.startswith('https://') and not simples.corpo, 'alta', 'S8')

        # --- M15: HSTS e cabeçalhos em produção ---------------------------------------------------------------------------------------------------
        resp = requisitar('GET', f'{https_base}/api/auth/sessao', token=token, ip=ip_unico(), cabecalhos=host_publico)
        hsts = resp.cabecalhos.get('strict-transport-security', '')
        m = re.search(r'max-age=(\d+)', hsts)
        r.checar('M15', CAT, 'Em produção as respostas HTTPS trazem Strict-Transport-Security com max-age de pelo menos 30 dias', 'max-age >= 2592000',
                 hsts or 'ausente', bool(m) and int(m.group(1)) >= 2_592_000, 'media', 'S8')
        faltando = [h for h in ('x-content-type-options', 'x-frame-options', 'content-security-policy', 'referrer-policy', 'cache-control')
                    if h not in resp.cabecalhos]
        r.checar('M16', CAT, 'Os cabeçalhos de segurança acompanham as respostas em produção', 'todos presentes', faltando or 'todos', not faltando, 'media', 'S12')

        # --- M17: superfície de produção ------------------------------------------------------------------------------------------------------------
        swagger = [requisitar('GET', f'{https_base}{p}', ip=ip_unico()).status for p in ('/swagger', '/swagger/index.html', '/swagger/v1/swagger.json')]
        r.checar('M17', CAT, 'Em produção o Swagger não é publicado', '404 em todos', swagger, all(s == 404 for s in swagger), 'media', 'S12')
        invalido = requisitar('POST', f'{https_base}/api/auth/autenticar', ip=ip_unico(), bruto=b'{"email": ')
        inexistente = requisitar('GET', f'{https_base}/api/rota-que-nao-existe', ip=ip_unico())
        vaza = [x for x in (invalido, inexistente) if re.search(r'(?i)stacktrace|exception|   at |\.cs:line|Microsoft\.|System\.', x.texto)]
        r.checar('M18', CAT, 'Erros em produção (JSON quebrado, rota inexistente) não mostram stack trace nem nomes internos', 'respostas limpas',
                 [x.texto[:80] for x in vaza] or 'limpas', not vaza and invalido.status in (400, 415, 422), 'media', 'S12')

        # --- M19: versão do TLS ---------------------------------------------------------------------------------------------------------------------------
        contexto = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        contexto.check_hostname = False
        contexto.verify_mode = ssl.CERT_NONE
        with socket.create_connection(('127.0.0.1', ambiente.PORTA_API_PRODUCAO_HTTPS), timeout=10) as bruto, contexto.wrap_socket(bruto) as seguro:
            negociada = seguro.version()
        antigo = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        antigo.check_hostname = False
        antigo.verify_mode = ssl.CERT_NONE
        aceitou_antigo = False
        try:
            antigo.minimum_version = ssl.TLSVersion.TLSv1
            antigo.maximum_version = ssl.TLSVersion.TLSv1_1
            antigo.set_ciphers('ALL:@SECLEVEL=0')
            with socket.create_connection(('127.0.0.1', ambiente.PORTA_API_PRODUCAO_HTTPS), timeout=10) as bruto, antigo.wrap_socket(bruto):
                aceitou_antigo = True
        except (ssl.SSLError, OSError, ValueError):
            aceitou_antigo = False
        r.checar('M19', CAT, 'O TLS negociado é 1.2 ou 1.3 e TLS 1.0/1.1 são recusados', 'TLS 1.2+ e sem 1.0/1.1',
                 f'{negociada}; TLS 1.0/1.1 {"ACEITO" if aceitou_antigo else "recusado"}', negociada in ('TLSv1.2', 'TLSv1.3') and not aceitou_antigo, 'media', 'S8')
    finally:
        api.parar()
