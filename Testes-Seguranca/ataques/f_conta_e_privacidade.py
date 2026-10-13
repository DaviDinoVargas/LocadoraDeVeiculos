"""Conta e privacidade: consentimento do rosto (S1), segundo fator e recuperação de senha (S18), encerrar conta (S1), trilha (S14)."""
import base64
import datetime
import hashlib
import hmac
import struct
import time

from lib import ambiente
from lib.http import ip_unico, requisitar

CAT = 'Conta e privacidade'


def totp(chave: str, passo_extra: int = 0) -> str:
    """Código de 6 dígitos do aplicativo autenticador (RFC 6238, SHA-1, 30 s): o mesmo que o celular geraria."""
    limpa = chave.replace(' ', '').upper()
    limpa += '=' * (-len(limpa) % 8)
    segredo = base64.b32decode(limpa)
    contador = struct.pack('>Q', int(time.time()) // 30 + passo_extra)
    h = hmac.new(segredo, contador, hashlib.sha1).digest()
    o = h[-1] & 0x0F
    return f'{(struct.unpack(">I", h[o:o + 4])[0] & 0x7FFFFFFF) % 1_000_000:06d}'


def _login(base, email, senha, codigo=None):
    corpo = {'email': email, 'senha': senha}
    if codigo is not None:
        corpo['codigoAutenticador'] = codigo
    return requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), json_corpo=corpo)


def executar(ctx):
    r, base = ctx.reg, ctx.base
    emp = ambiente.nova_empresa(base, 'priv')
    ctx.lembrar(emp.senha, emp.token)

    # --- F01/F02: consentimento do rosto (S1) -----------------------------------------------------------------------
    anonimo = requisitar('GET', f'{base}/api/auth/rosto/consentimento', ip=ip_unico())
    termo = requisitar('GET', f'{base}/api/auth/rosto/consentimento', token=emp.token, ip=ip_unico())
    dados = termo.json() if termo.ok else {}
    r.checar('F01', CAT, 'O termo de consentimento tem versão e texto e só é servido a quem está logado',
             'anônimo 401; logado 200 com versao/texto', (anonimo.status, termo.status),
             anonimo.status == 401 and termo.ok and bool(dados.get('versao')) and len(dados.get('texto', '')) > 200, 'media', 'S1')

    sem_aceite = requisitar('POST', f'{base}/api/auth/rosto', token=emp.token, ip=ip_unico(), json_corpo={'imagemBase64': 'x'})
    versao_velha = requisitar('POST', f'{base}/api/auth/rosto', token=emp.token, ip=ip_unico(),
                              json_corpo={'imagemBase64': 'x', 'consentimento': True, 'versaoDoTexto': 'v0-antiga'})
    r.checar('F02', CAT, 'Cadastrar o rosto sem aceitar a versão atual do termo é recusado (nem chega ao ML)', '400 nos dois casos',
             (sem_aceite.status, versao_velha.status), sem_aceite.status == 400 and versao_velha.status == 400, 'alta', 'S1')

    # --- F03: 2FA exige login ---------------------------------------------------------------------------------------
    rotas = [('GET', '/api/auth/2fa'), ('POST', '/api/auth/2fa/iniciar'), ('POST', '/api/auth/2fa/ativar'),
             ('POST', '/api/auth/2fa/desativar'), ('POST', '/api/auth/encerrar-conta'), ('POST', '/api/auth/alterar-senha')]
    anon = [(m, p, requisitar(m, f'{base}{p}', ip=ip_unico(), json_corpo={} if m == 'POST' else None).status) for m, p in rotas]
    r.checar('F03', CAT, 'Segundo fator, troca de senha e encerrar conta não existem para anônimos', '401 em todas',
             [x for x in anon if x[2] != 401] or '401', all(x[2] == 401 for x in anon), 'alta', 'S18')

    # --- F04/F05: recuperação de senha sem enumeração -----------------------------------------------------------------
    existente = requisitar('POST', f'{base}/api/auth/esqueci-senha', ip=ip_unico(), json_corpo={'email': emp.email})
    inexistente = requisitar('POST', f'{base}/api/auth/esqueci-senha', ip=ip_unico(), json_corpo={'email': f'ninguem-{ambiente.secrets.token_hex(4)}@teste.local'})
    r.checar('F04', CAT, '"Esqueci minha senha" responde igual para e-mail com e sem conta', 'mesmo status e mesmo corpo',
             (existente.status, inexistente.status), existente.status == inexistente.status == 202 and existente.texto == inexistente.texto,
             'alta', 'S18')

    token_falso = base64.urlsafe_b64encode(b'token-inventado-pelo-atacante').decode().rstrip('=')
    redef_a = requisitar('POST', f'{base}/api/auth/redefinir-senha', ip=ip_unico(),
                         json_corpo={'email': emp.email, 'token': token_falso, 'novaSenha': 'Nova@12345', 'confirmarNovaSenha': 'Nova@12345'})
    redef_b = requisitar('POST', f'{base}/api/auth/redefinir-senha', ip=ip_unico(),
                         json_corpo={'email': 'ninguem@teste.local', 'token': token_falso, 'novaSenha': 'Nova@12345', 'confirmarNovaSenha': 'Nova@12345'})
    r.checar('F05', CAT, 'Redefinir com token inventado falha igual para e-mail com e sem conta', '400 e mesma mensagem',
             (redef_a.status, redef_b.status), redef_a.status == redef_b.status == 400 and redef_a.texto == redef_b.texto, 'alta', 'S18')
    ainda = _login(base, emp.email, emp.senha)
    r.checar('F05b', CAT, 'A senha da conta continua a mesma depois do ataque de redefinição', '200', ainda.status, ainda.ok, 'alta', 'S18')

    # --- F10-F16: segundo fator de ponta a ponta ------------------------------------------------------------------------
    conta = ambiente.nova_empresa(base, 'dois')
    ctx.lembrar(conta.senha, conta.token)
    iniciar = requisitar('POST', f'{base}/api/auth/2fa/iniciar', token=conta.token, ip=ip_unico())
    chave = (iniciar.json() or {}).get('chave', '') if iniciar.ok else ''
    ctx.lembrar(chave)
    r.checar('F10', CAT, 'Iniciar o 2FA devolve a chave do aplicativo', '200 com chave', iniciar.status, iniciar.ok and len(chave) >= 16, 'media', 'S18')
    if not chave:
        return

    errado = requisitar('POST', f'{base}/api/auth/2fa/ativar', token=conta.token, ip=ip_unico(), json_corpo={'codigo': '000000'})
    r.checar('F11', CAT, 'Ativar o 2FA com código errado é recusado e não liga o 2FA', '400', errado.status, errado.status == 400, 'alta', 'S18')
    sem_2fa = _login(base, conta.email, conta.senha)
    r.checar('F11b', CAT, 'Enquanto o 2FA não foi confirmado, o login segue só com a senha', '200', sem_2fa.status, sem_2fa.ok, 'media', 'S18')

    ativar = requisitar('POST', f'{base}/api/auth/2fa/ativar', token=conta.token, ip=ip_unico(), json_corpo={'codigo': totp(chave)})
    codigos = (ativar.json() or {}).get('codigos', []) if ativar.ok else []
    ctx.lembrar(*codigos)
    r.checar('F12', CAT, 'Ativar com o código certo devolve 10 códigos de recuperação', '200 com 10 códigos', (ativar.status, len(codigos)),
             ativar.ok and len(codigos) == 10, 'media', 'S18')
    if len(codigos) != 10:
        return

    pede = _login(base, conta.email, conta.senha)
    r.checar('F13', CAT, 'Com o 2FA ativo, senha certa sem código é 428 (pede o código) e não entrega token', '428 sem token',
             pede.status, pede.status == 428 and '"chave"' not in pede.texto, 'alta', 'S18')

    errado_login = _login(base, conta.email, conta.senha, '000000')
    r.checar('F14', CAT, 'Senha certa com código errado falha com a mesma mensagem genérica de senha errada', '400 "Usuário ou senha inválidos."',
             errado_login.status, errado_login.status == 400 and 'Usuário ou senha inválidos' in errado_login.texto, 'alta', 'S18')

    certo = _login(base, conta.email, conta.senha, totp(chave))
    r.checar('F15', CAT, 'Senha e código do aplicativo juntos entram', '200', certo.status, certo.ok, 'alta', 'S18')

    sem_senha = _login(base, conta.email, 'Errada@123456', totp(chave))
    r.checar('F15b', CAT, 'Código certo com senha errada não entra (o segundo fator não substitui a senha)', '400', sem_senha.status,
             sem_senha.status == 400, 'alta', 'S18')

    recup = _login(base, conta.email, conta.senha, codigos[0])
    reusado = _login(base, conta.email, conta.senha, codigos[0])
    r.checar('F16', CAT, 'Código de recuperação entra uma vez só', '1ª 200 / 2ª 400', (recup.status, reusado.status),
             recup.ok and reusado.status == 400, 'alta', 'S18')

    # o segundo fator vale também para o login facial (nenhum caminho dispensa o código)
    facial = requisitar('POST', f'{base}/api/auth/entrar-facial', ip=ip_unico(), json_corpo={'email': conta.email, 'imagemBase64': 'x'})
    r.checar('F17', CAT, 'Login facial de conta com 2FA não vira atalho que dispensa o código', '4xx (nunca 200)', facial.status,
             400 <= facial.status < 500, 'alta', 'S18')

    # --- F20-F25: encerrar a conta (S1) ---------------------------------------------------------------------------------------
    dona = ambiente.nova_empresa(base, 'fim')
    ctx.lembrar(dona.senha, dona.token)
    senha_f = ambiente.senha_forte()
    email_f = f'func-{ambiente.secrets.token_hex(4)}@teste.local'
    ctx.lembrar(senha_f)
    criar = requisitar('POST', f'{base}/api/funcionarios', token=dona.token, ip=ip_unico(), json_corpo={
        'nomeCompleto': 'Funcionario de Teste', 'cpf': ambiente.cpf_valido(), 'email': email_f, 'senha': senha_f,
        'confirmarSenha': senha_f, 'salario': 2500, 'admissaoEmUtc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    func = ambiente.conta_do_login(base, email_f, senha_f, 'Funcionario') if criar.ok else None
    if func:
        ctx.lembrar(func.token)
        func_tenta = requisitar('POST', f'{base}/api/auth/encerrar-conta', token=func.token, ip=ip_unico(), json_corpo={'senha': senha_f})
        r.checar('F20', CAT, 'Funcionário não encerra a conta da empresa', '403', func_tenta.status, func_tenta.status == 403, 'alta', 'S1')

    senha_errada = requisitar('POST', f'{base}/api/auth/encerrar-conta', token=dona.token, ip=ip_unico(), json_corpo={'senha': 'Errada@123456'})
    segue = _login(base, dona.email, dona.senha)
    r.checar('F21', CAT, 'Encerrar a conta com a senha errada é recusado e a conta continua ativa', '400 e login 200',
             (senha_errada.status, segue.status), senha_errada.status == 400 and segue.ok, 'alta', 'S1')

    # token roubado sozinho não basta: sem a senha, nada de encerrar
    sem_senha_corpo = requisitar('POST', f'{base}/api/auth/encerrar-conta', token=dona.token, ip=ip_unico(), json_corpo={})
    r.checar('F22', CAT, 'Token sem a senha não encerra a conta', '4xx', sem_senha_corpo.status, 400 <= sem_senha_corpo.status < 500, 'alta', 'S1')

    fecha = requisitar('POST', f'{base}/api/auth/encerrar-conta', token=dona.token, ip=ip_unico(), json_corpo={'senha': dona.senha})
    r.checar('F23', CAT, 'Encerrar a conta com a senha certa funciona', '2xx', fecha.status, fecha.ok, 'alta', 'S1')
    if fecha.ok:
        depois = _login(base, dona.email, dona.senha)
        velho = requisitar('GET', f'{base}/api/dashboard/resumo', token=dona.token, ip=ip_unico())
        r.checar('F24', CAT, 'Conta encerrada não entra mais e o token antigo deixa de valer', 'login 4xx e token 401',
                 (depois.status, velho.status), 400 <= depois.status < 500 and velho.status == 401, 'alta', 'S1')
        if func:
            f_depois = _login(base, email_f, senha_f)
            r.checar('F25', CAT, 'Os funcionários da empresa encerrada também não entram', '4xx', f_depois.status,
                     400 <= f_depois.status < 500, 'alta', 'S1')

    # --- F30: a trilha mostra as mudanças de segurança, mas nunca segredos -------------------------------------------------------
    login = _login(base, conta.email, conta.senha, totp(chave, 0))
    token = (login.json() or {}).get('chave') if login.ok else None
    if token:
        trilha = requisitar('GET', f'{base}/api/auditoria?limite=200', token=token, ip=ip_unico()).texto
        r.checar('F30', CAT, 'A ativação do 2FA aparece na trilha da empresa', 'DoisFatoresAtivado presente', 'presente' if 'DoisFatoresAtivado' in trilha else 'ausente',
                 'DoisFatoresAtivado' in trilha, 'media', 'S14')
        segredos = [s for s in [chave.replace(' ', ''), conta.senha, *codigos] if s and s in trilha.replace(' ', '')]
        r.checar('F31', CAT, 'A trilha nunca guarda a chave do 2FA, códigos de recuperação nem senhas', 'nenhum segredo na trilha',
                 'vazou' if segredos else 'ok', not segredos, 'alta', 'S14')
