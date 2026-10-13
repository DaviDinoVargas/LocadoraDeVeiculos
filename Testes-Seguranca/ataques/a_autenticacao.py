"""Ataques à autenticação e à sessão: JWT forjado, força bruta, bloqueio, reuso de refresh token, enumeração, rate limit."""
import statistics

from lib import ambiente
from lib.http import ip_unico, jwt_forjado, requisitar

CAT = 'Autenticação'
GENERICA = 'inválid'  # trecho da mensagem genérica de falha de login


def executar(ctx):
    r, base = ctx.reg, ctx.base

    empresa = ambiente.nova_empresa(base, 'auth')
    ctx.lembrar(empresa.senha, empresa.token)
    token = empresa.token

    # --- A01: sem credencial -----------------------------------------------------------------
    resp = requisitar('GET', f'{base}/api/dashboard/resumo', ip=ip_unico())
    r.checar('A01', CAT, 'Rota protegida sem token é recusada', '401', resp.status, resp.status == 401, 'alta')

    # --- A02-A04: JWT forjado ----------------------------------------------------------------
    adulterado = jwt_forjado(token, alterar_claims={'EmpresaId': '00000000-0000-0000-0000-000000000001'})
    resp = requisitar('GET', f'{base}/api/dashboard/resumo', token=adulterado, ip=ip_unico())
    r.checar('A02', CAT, 'JWT com payload alterado (assinatura antiga) é recusado', '401', resp.status, resp.status == 401, 'alta')

    sem_alg = jwt_forjado(token, alg='none', assinatura='')
    resp = requisitar('GET', f'{base}/api/dashboard/resumo', token=sem_alg, ip=ip_unico())
    r.checar('A03', CAT, 'JWT com alg=none é recusado', '401', resp.status, resp.status == 401, 'alta')

    outra_chave = jwt_forjado(token, chave_hs256=b'chave-do-atacante-com-mais-de-32-caracteres!!')
    resp = requisitar('GET', f'{base}/api/dashboard/resumo', token=outra_chave, ip=ip_unico())
    r.checar('A04', CAT, 'JWT assinado com outra chave é recusado', '401', resp.status, resp.status == 401, 'alta')

    escalada = jwt_forjado(token, alterar_claims={'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Empresa'},
                           chave_hs256=b'x' * 40)
    resp = requisitar('GET', f'{base}/api/auditoria', token=escalada, ip=ip_unico())
    r.checar('A05', CAT, 'Escalada de papel com assinatura falsa é recusada', '401', resp.status, resp.status == 401, 'alta')

    # --- A06-A07: logout e reuso de refresh token --------------------------------------------
    sessao = ambiente.conta_do_login(base, empresa.email, empresa.senha, 'Empresa')
    ctx.lembrar(sessao.token)
    sair = requisitar('POST', f'{base}/api/auth/sair', cookie=sessao.cookie, token=sessao.token, ip=ip_unico(),
                      cabecalhos={'X-Locadora-Client': 'web'})
    r.checar('A06a', CAT, 'Logout com cookie e cabeçalho do cliente funciona', '204', sair.status, sair.status in (200, 204))
    depois = requisitar('POST', f'{base}/api/auth/rotacionar', cookie=sessao.cookie, ip=ip_unico(), cabecalhos={'X-Locadora-Client': 'web'})
    r.checar('A06b', CAT, 'Refresh token não rotaciona depois do logout', '401', depois.status, depois.status == 401, 'alta')
    acesso = requisitar('GET', f'{base}/api/dashboard/resumo', token=sessao.token, ip=ip_unico())
    r.checar('A06c', CAT, 'Access token deixa de valer depois do logout', '401', acesso.status, acesso.status == 401, 'media', 'S26/S29')

    conta = ambiente.conta_do_login(base, empresa.email, empresa.senha, 'Empresa')
    ctx.lembrar(conta.token)
    h = {'X-Locadora-Client': 'web'}
    primeira = requisitar('POST', f'{base}/api/auth/rotacionar', cookie=conta.cookie, ip=ip_unico(), cabecalhos=h)
    novo_cookie = ambiente._extrair_cookie_refresh(primeira.cabecalhos.get('set-cookie', ''))
    r.checar('A07a', CAT, 'Rotação normal do refresh token funciona', '200', primeira.status, primeira.ok and novo_cookie is not None)
    reuso = requisitar('POST', f'{base}/api/auth/rotacionar', cookie=conta.cookie, ip=ip_unico(), cabecalhos=h)
    r.checar('A07b', CAT, 'Reuso do refresh token antigo é recusado', '401', reuso.status, reuso.status == 401, 'alta')
    if novo_cookie:
        filho = requisitar('POST', f'{base}/api/auth/rotacionar', cookie=novo_cookie, ip=ip_unico(), cabecalhos=h)
        r.checar('A07c', CAT, 'Detectado o reuso, a sessão inteira é derrubada (o token novo também deixa de valer)',
                 '401', filho.status, filho.status == 401, 'media', 'S29')

    # --- A08-A10: força bruta, bloqueio e mensagens ------------------------------------------
    alvo = ambiente.nova_empresa(base, 'bf')
    ctx.lembrar(alvo.senha, alvo.token)
    statuses, mensagens = [], set()
    for _ in range(5):
        resp = ambiente.login(base, alvo.email, 'Errada@123456')
        statuses.append(resp.status)
        mensagens.add(resp.texto.strip())
    r.checar('A08a', CAT, 'Senha errada devolve 400/401 (nunca 500)', '4xx', statuses, all(400 <= s < 500 for s in statuses), 'alta')
    depois_bloqueio = ambiente.login(base, alvo.email, alvo.senha)
    r.checar('A08b', CAT, '5 falhas bloqueiam a conta: a senha CERTA é recusada em seguida', '4xx',
             depois_bloqueio.status, 400 <= depois_bloqueio.status < 500, 'alta')
    desconhecido = ambiente.login(base, f'nao-existe-{ip_unico()}@teste.local', 'Qualquer@12345')
    errada = ambiente.login(base, ambiente.nova_empresa(base, 'msg').email, 'Errada@123456')
    r.checar('A09', CAT, 'Conta bloqueada responde igual a senha errada (não revela que o e-mail existe)',
             'mensagem idêntica', f'bloqueada="{depois_bloqueio.texto[:60]}" errada="{errada.texto[:60]}"',
             depois_bloqueio.texto.strip() == errada.texto.strip() == desconhecido.texto.strip(), 'media', 'S28/S20')

    # --- A10: enumeração de usuários por tempo de resposta -----------------------------------
    existente = ambiente.nova_empresa(base, 'tempo')
    ctx.lembrar(existente.senha)
    t_exist, t_inexist = [], []
    for _ in range(5):
        t_exist.append(ambiente.login(base, existente.email, 'Errada@123456').ms)
        t_inexist.append(ambiente.login(base, f'x-{ip_unico()}@teste.local', 'Errada@123456').ms)
    me, mi = statistics.median(t_exist), statistics.median(t_inexist)
    r.checar('A10', CAT, 'Tempo de login igual para e-mail existente e inexistente (sem enumeração por tempo)',
             'razão < 3x', f'existente {me:.0f} ms x inexistente {mi:.0f} ms', me < 3 * max(mi, 1), 'media', 'S20')

    # --- A11: enumeração pelo cadastro -------------------------------------------------------
    senha2 = ambiente.senha_forte()
    dup = requisitar('POST', f'{base}/api/auth/registrar', ip=ip_unico(),
                     json_corpo={'nomeCompleto': 'X', 'email': existente.email, 'senha': senha2, 'confirmarSenha': senha2})
    ctx.lembrar(senha2)
    revela = dup.status == 409 or 'já existe' in dup.texto.lower() or 'j\u00e1 existe' in dup.texto.lower()
    r.checar('A11', CAT, 'Cadastro de e-mail já existente não revela que ele existe (nem por status nem por mensagem)',
             'resposta neutra', f'{dup.status} {dup.texto[:80]}', not revela, 'media', 'S20/S6')

    # --- A12-A14: rate limit ------------------------------------------------------------------
    ip = ip_unico()
    codigos = [ambiente.login(base, existente.email, 'Errada@123456', ip=ip).status for _ in range(12)]
    r.checar('A12', CAT, 'Login: o 11º pedido do mesmo IP no minuto recebe 429', '429 a partir do 11º',
             codigos, 429 in codigos[10:] and 429 not in codigos[:10], 'media', 'S10')

    ip = ip_unico()
    corpo = {'nomeCompleto': '', 'email': '', 'senha': '', 'confirmarSenha': ''}
    codigos = [requisitar('POST', f'{base}/api/auth/registrar', ip=ip, json_corpo=corpo).status for _ in range(7)]
    r.checar('A13', CAT, 'Cadastro: o 6º pedido do mesmo IP na hora recebe 429', '429 a partir do 6º',
             codigos, 429 in codigos[5:] and 429 not in codigos[:5], 'media', 'S6')

    ip = ip_unico()
    ctr = ambiente.conta_do_login(base, empresa.email, empresa.senha, 'Empresa')
    ctx.lembrar(ctr.token)
    codigos = [requisitar('GET', f'{base}/api/dashboard/resumo', token=ctr.token, ip=ip).status for _ in range(330)]
    r.checar('A14', CAT, 'Limite global: depois de ~300 pedidos/min do mesmo usuário vem 429 (rota de negócio)', '429 após ~300',
             f'{codigos.count(429)} respostas 429 de 330', codigos.count(429) > 0 and codigos[:250].count(429) == 0, 'media', 'S10')

    # --- A15: o bloqueio de conta não pode ser usado como arma por quem não é o dono (S28) ---------------
    vitima = ambiente.nova_empresa(base, 'vitima')
    ctx.lembrar(vitima.senha)
    for _ in range(6):
        ambiente.login(base, vitima.email, 'Errada@123456', ip=ip_unico())          # atacante (IPs diferentes)
    dono = ambiente.login(base, vitima.email, vitima.senha, ip=ip_unico())          # o dono, de outro lugar
    r.pendente('A15', CAT, 'Bloqueio de conta permite DoS dirigido (risco aceito)',
               f'após 6 erros vindos de IPs diferentes o dono {"conseguiu" if dono.ok else "NÃO conseguiu"} entrar (status {dono.status}); '
               'é o preço da proteção contra força bruta distribuída, mitigado pela mensagem genérica e pelo limite por IP', 'S28')

    # --- A16: cadastro exige confirmação de que o e-mail é de quem cadastrou (S6) -----------------------
    novo = ambiente.nova_empresa(base, 'confirma')
    ctx.lembrar(novo.senha)
    entrou = ambiente.login(base, novo.email, novo.senha)
    if 400 <= entrou.status < 500:
        r.checar('A16', CAT, 'Conta recém-cadastrada só entra depois de confirmar o e-mail', '4xx antes da confirmação', entrou.status, True, 'media', 'S6')
    else:
        r.pendente('A16', CAT, 'Conta recém-cadastrada entra sem confirmar o e-mail (depende de e-mail transacional)', entrou.status, 'S6')

    # --- A17: duas rotações simultâneas do mesmo refresh token: só uma vale (S29) -------------------------
    from concurrent.futures import ThreadPoolExecutor
    cx = ambiente.conta_do_login(base, empresa.email, empresa.senha, 'Empresa')
    ctx.lembrar(cx.token)
    h = {'X-Locadora-Client': 'web'}
    with ThreadPoolExecutor(max_workers=6) as pool:
        resultados = list(pool.map(lambda _: requisitar('POST', f'{base}/api/auth/rotacionar', cookie=cx.cookie, ip=ip_unico(), cabecalhos=h).status, range(6)))
    r.checar('A17', CAT, 'Rotação simultânea do mesmo refresh token: no máximo uma tem sucesso', '<= 1 resposta 200',
             resultados, resultados.count(200) <= 1, 'media', 'S29')

    # --- A18: o limite por IP vale pelo prefixo /64 em IPv6 (S10) ---------------------------------------------
    prefixo = f"2001:db8:{ambiente.secrets.randbelow(65535):x}:{ambiente.secrets.randbelow(65535):x}"
    codigos = [ambiente.login(base, vitima.email, 'Errada@123456', ip=f'{prefixo}::{i + 1:x}').status for i in range(12)]
    r.checar('A18', CAT, 'Trocar o final do endereço IPv6 a cada pedido não escapa do limite de login', '429 a partir do 11º',
             codigos, 429 in codigos[10:] and 429 not in codigos[:10], 'media', 'S10')

