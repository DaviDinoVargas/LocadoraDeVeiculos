"""Ataques por entradas maliciosas: injeção de SQL, XSS armazenado, corpo gigante/malformado, validação de documentos e foto."""
import datetime

from lib import ambiente
from lib.http import id_de, ip_unico, requisitar

CAT = 'Entradas'

SQLI = ["' OR '1'='1", "'; DROP TABLE Clientes;--", "\" OR 1=1 --", "1; WAITFOR DELAY '0:0:5'--", "admin'--"]
XSS = ['<script>alert(1)</script>', '"><img src=x onerror=alert(1)>', "javascript:alert(1)"]
VAZAMENTO = ('SqlException', 'System.', 'Microsoft.', 'StackTrace', 'at LocadoraDeVeiculos', 'Npgsql', 'EntityFramework')


def _cliente(nome, cpf=None, email='c@teste.local'):
    return {'nome': nome, 'cpf': cpf or ambiente.cpf_valido(), 'rg': '123456789', 'cnh': '12345678901',
            'validadeCnh': (datetime.datetime.now() + datetime.timedelta(days=900)).isoformat(),
            'telefone': '(11) 99999-0000', 'email': email, 'endereco': 'Rua Exemplo X, 100, Centro'}


def executar(ctx):
    r, base = ctx.reg, ctx.base
    emp = ambiente.nova_empresa(base, 'ent')
    ctx.lembrar(emp.senha, emp.token)

    # --- C01: SQL injection --------------------------------------------------------------------
    problemas = []
    for payload in SQLI:
        # login
        resp = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), json_corpo={'email': payload, 'senha': payload})
        if resp.status >= 500 or any(v in resp.texto for v in VAZAMENTO) or resp.ok:
            problemas.append(f'login:{payload[:12]}->{resp.status}')
        # campo de texto e parâmetros de rota/consulta
        resp = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp.token, ip=ip_unico(), json_corpo=_cliente(payload))
        if resp.status >= 500 or any(v in resp.texto for v in VAZAMENTO):
            problemas.append(f'cliente:{payload[:12]}->{resp.status}')
        resp = requisitar('GET', f'{base}/api/auditoria?limite={payload}', token=emp.token, ip=ip_unico())
        if resp.status >= 500 or any(v in resp.texto for v in VAZAMENTO):
            problemas.append(f'query:{payload[:12]}->{resp.status}')
        resp = requisitar('GET', f'{base}/api/clientes/{payload}', token=emp.token, ip=ip_unico())
        if resp.status >= 500 or any(v in resp.texto for v in VAZAMENTO):
            problemas.append(f'rota:{payload[:12]}->{resp.status}')
    r.checar('C01', CAT, 'Payloads de SQL injection não causam 500, vazamento de erro nem login', 'nenhum problema',
             problemas or 'nenhum', not problemas, 'alta')

    tempo = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(),
                       json_corpo={'email': "x'; WAITFOR DELAY '0:0:5'--", 'senha': 'Qualquer@12345'})
    r.checar('C02', CAT, 'Injeção por tempo (WAITFOR) não atrasa a resposta', '< 3 s', f'{tempo.ms:.0f} ms', tempo.ms < 3000, 'alta')

    # --- C03: XSS armazenado --------------------------------------------------------------------
    nome = XSS[0]
    criado = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp.token, ip=ip_unico(), json_corpo=_cliente(nome))
    lista = requisitar('GET', f'{base}/api/clientes', token=emp.token, ip=ip_unico())
    tipo = lista.cabecalhos.get('content-type', '')
    r.checar('C03a', CAT, 'XSS: a API devolve JSON (nunca HTML) com nosniff', 'application/json + nosniff',
             f"{tipo} / {lista.cabecalhos.get('x-content-type-options')}",
             'application/json' in tipo and lista.cabecalhos.get('x-content-type-options') == 'nosniff', 'media')
    r.checar('C03b', CAT, 'XSS: o texto armazenado volta com "<" escapado no JSON (\\u003C)', 'sem "<script>" literal',
             'literal' if '<script>' in lista.texto else 'escapado', criado.ok is False or '<script>' not in lista.texto, 'media')

    # --- C04-C07: corpos e tipos --------------------------------------------------------------
    grande = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), bruto=b'{"email":"' + b'A' * (3 * 1024 * 1024) + b'"}')
    r.checar('C04', CAT, 'Corpo de 3 MB é recusado (limite de 2 MB)', '413 ou conexão encerrada',
             grande.erro or grande.status, bool(grande.erro) or grande.status in (400, 413), 'media', 'S9')

    mal = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), bruto=b'{"email": ')
    r.checar('C05a', CAT, 'JSON malformado devolve 400 sem stack trace', '400 limpo', mal.status,
             mal.status == 400 and not any(v in mal.texto for v in VAZAMENTO), 'media')
    tipos = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), json_corpo={'email': 123, 'senha': ['a']})
    r.checar('C05b', CAT, 'Tipos errados devolvem 4xx sem stack trace', '4xx limpo', tipos.status,
             400 <= tipos.status < 500 and not any(v in tipos.texto for v in VAZAMENTO), 'media')
    nulos = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), json_corpo={'email': None, 'senha': None})
    r.checar('C05c', CAT, 'Campos nulos devolvem 4xx (nunca 500)', '4xx', nulos.status, 400 <= nulos.status < 500, 'media')
    ct = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), bruto=b'email=a&senha=b',
                    cabecalhos={'Content-Type': 'application/x-www-form-urlencoded'})
    r.checar('C06', CAT, 'Content-Type inesperado devolve 4xx (415)', '4xx', ct.status, 400 <= ct.status < 500, 'baixa')
    trace = requisitar('TRACE', f'{base}/api/clientes', ip=ip_unico())
    r.checar('C07', CAT, 'Método TRACE não é aceito', '4xx', trace.erro or trace.status,
             bool(trace.erro) or trace.status in (400, 404, 405, 501), 'baixa')

    # --- C10-C12: validação de documentos e foto -----------------------------------------------
    cpf_ruim = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp.token, ip=ip_unico(), json_corpo=_cliente('Cliente CPF', cpf='123.456.789-00'))
    r.checar('C10', CAT, 'CPF com dígito verificador inválido é recusado pelo servidor', '400', cpf_ruim.status, cpf_ruim.status == 400, 'media', 'S13')

    grupo = requisitar('POST', f'{base}/api/grupos-automovel', token=emp.token, ip=ip_unico(), json_corpo={'nome': 'Grupo foto', 'descricao': 'x'})
    gid = id_de(grupo)
    def automovel(foto, placa):
        return requisitar('POST', f'{base}/api/automoveis', token=emp.token, ip=ip_unico(), json_corpo={
            'placa': placa, 'marca': 'Marca', 'cor': 'Preto', 'modelo': 'Modelo', 'tipoCombustivel': 'Flex',
            'capacidadeTanque': 50, 'ano': 2022, 'foto': foto, 'grupoAutomovelId': gid})
    perigosas = {'javascript:alert(1)': 'ABC1D23', 'https://rastreador.exemplo/pixel.png?u=1': 'ABC1D24', 'file:///c:/windows/win.ini': 'ABC1D25'}
    aceitas = [f for f, placa in perigosas.items() if automovel(f, placa).ok]
    r.checar('C11', CAT, 'Foto do veículo com esquema perigoso ou URL externa é recusada', 'todas recusadas', aceitas or 'todas recusadas',
             not aceitas, 'baixa', 'S31')
    enorme = automovel('data:image/png;base64,' + 'A' * (1_500_000), 'ABC1D26')
    r.checar('C12', CAT, 'Foto gigante (1,5 MB) é recusada', '400', enorme.status, enorme.status == 400, 'baixa', 'S31')
