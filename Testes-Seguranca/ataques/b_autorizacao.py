"""Ataques de autorização: papéis, isolamento entre empresas (IDOR), mass assignment, funcionário desligado, auditoria."""
import datetime

from lib import ambiente
from lib.http import id_de as _id, ip_unico, requisitar

CAT = 'Autorização'

SO_EMPRESA = ['/api/auditoria', '/api/funcionarios', '/api/grupos-automovel', '/api/configuracoes', '/api/parceiros']
COMPARTILHADAS = ['/api/alugueis', '/api/automoveis', '/api/clientes', '/api/condutores', '/api/cupons',
                  '/api/desafios-cupom', '/api/devolucoes', '/api/dashboard/resumo']


def _criar_funcionario(base, empresa):
    senha = ambiente.senha_forte()
    email = f'func-{ambiente.secrets.token_hex(4)}@teste.local'
    resp = requisitar('POST', f'{base}/api/funcionarios', token=empresa.token, ip=ip_unico(), json_corpo={
        'nomeCompleto': 'Funcionario de Teste', 'cpf': ambiente.cpf_valido(), 'email': email, 'senha': senha,
        'confirmarSenha': senha, 'salario': 2500, 'admissaoEmUtc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    return resp, email, senha


def executar(ctx):
    r, base = ctx.reg, ctx.base
    emp_a = ambiente.nova_empresa(base, 'A')
    emp_b = ambiente.nova_empresa(base, 'B')
    ctx.lembrar(emp_a.senha, emp_b.senha, emp_a.token, emp_b.token)

    # --- B01-B03: papéis ---------------------------------------------------------------------
    resp_f, email_f, senha_f = _criar_funcionario(base, emp_a)
    ctx.lembrar(senha_f)
    r.checar('B00', CAT, 'Empresa cria um funcionário', '2xx', resp_f.status, resp_f.ok and _id(resp_f) is not None, 'alta')
    func = ambiente.conta_do_login(base, email_f, senha_f, 'Funcionario')
    ctx.lembrar(func.token)

    anonimos = [(rota, requisitar('GET', f'{base}{rota}', ip=ip_unico()).status) for rota in SO_EMPRESA + COMPARTILHADAS]
    r.checar('B01', CAT, 'Anônimo recebe 401 em todas as rotas de negócio', '401 em todas',
             [x for x in anonimos if x[1] != 401], all(s == 401 for _, s in anonimos), 'alta')

    proibidas = [(rota, requisitar('GET', f'{base}{rota}', token=func.token, ip=ip_unico()).status) for rota in SO_EMPRESA]
    r.checar('B02', CAT, 'Funcionário recebe 403 nas rotas só da Empresa', '403 em todas',
             [x for x in proibidas if x[1] != 403], all(s == 403 for _, s in proibidas), 'alta')

    escritas = []
    for rota in ['/api/automoveis', '/api/cupons', '/api/desafios-cupom', '/api/parceiros', '/api/funcionarios', '/api/grupos-automovel']:
        escritas.append((rota, requisitar('POST', f'{base}{rota}', token=func.token, ip=ip_unico(), json_corpo={}).status))
    r.checar('B03', CAT, 'Funcionário não cria recursos reservados à Empresa', '403 em todas',
             [x for x in escritas if x[1] != 403], all(s == 403 for _, s in escritas), 'alta')

    taxa = requisitar('POST', f'{base}/api/taxas-servicos', token=func.token, ip=ip_unico(), json_corpo={})
    r.checar('B04a', CAT, 'Funcionário não cria taxas de serviço (preço é decisão da gerência)', '403', taxa.status, taxa.status == 403, 'media', 'S32')
    taxa_put = requisitar('PUT', f'{base}/api/taxas-servicos/00000000-0000-0000-0000-000000000001', token=func.token, ip=ip_unico(), json_corpo={})
    taxa_del = requisitar('DELETE', f'{base}/api/taxas-servicos/00000000-0000-0000-0000-000000000001', token=func.token, ip=ip_unico())
    r.checar('B04b', CAT, 'Funcionário não edita nem exclui taxas de serviço', '403 em PUT e DELETE', (taxa_put.status, taxa_del.status),
             taxa_put.status == 403 and taxa_del.status == 403, 'media', 'S32')
    leitura = requisitar('GET', f'{base}/api/taxas-servicos', token=func.token, ip=ip_unico())
    r.checar('B04c', CAT, 'Funcionário continua LENDO as taxas (precisa delas para montar o aluguel)', '200', leitura.status, leitura.ok, 'media', 'S32')

    # --- B10-B16: isolamento entre empresas (IDOR) -------------------------------------------
    grupo = requisitar('POST', f'{base}/api/grupos-automovel', token=emp_a.token, ip=ip_unico(),
                       json_corpo={'nome': 'Grupo secreto da A', 'descricao': 'x'})
    gid = _id(grupo)
    cliente = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp_a.token, ip=ip_unico(), json_corpo={
        'nome': 'Cliente da A', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': '12345678901',
        'validadeCnh': (datetime.datetime.now() + datetime.timedelta(days=900)).isoformat(),
        'telefone': '(11) 99999-0000', 'email': 'cliente.a@teste.local', 'endereco': 'Rua Exemplo A, 123, Centro'})
    cid = _id(cliente)
    parceiro = requisitar('POST', f'{base}/api/parceiros', token=emp_a.token, ip=ip_unico(),
                          json_corpo={'nome': 'Parceiro da A', 'cnpj': ambiente.cnpj_valido(), 'categoria': 'Hotel'})
    pid = _id(parceiro)
    r.checar('B10', CAT, 'Empresa A cria grupo, cliente e parceiro de teste', '3 criados',
             [(x.status, x.texto[:120]) for x in (grupo, cliente, parceiro)], all([gid, cid, pid]), 'alta')

    recursos = [('grupos-automovel', gid, {'nome': 'invadido', 'descricao': 'x'}),
                ('clientes', cid, None), ('parceiros', pid, {'nome': 'invadido', 'cnpj': ambiente.cnpj_valido(), 'categoria': 'Hotel', 'ativo': True})]
    vazamentos = []
    for rota, rid, corpo_put in recursos:
        if not rid:
            continue
        got = requisitar('GET', f'{base}/api/{rota}/{rid}', token=emp_b.token, ip=ip_unico())
        if got.status not in (403, 404):
            vazamentos.append(f'GET {rota} -> {got.status}')
        if corpo_put:
            put = requisitar('PUT', f'{base}/api/{rota}/{rid}', token=emp_b.token, ip=ip_unico(), json_corpo=corpo_put)
            if put.status not in (403, 404):
                vazamentos.append(f'PUT {rota} -> {put.status}')
        delete = requisitar('DELETE', f'{base}/api/{rota}/{rid}', token=emp_b.token, ip=ip_unico())
        if delete.status not in (403, 404):
            vazamentos.append(f'DELETE {rota} -> {delete.status}')
    r.checar('B11', CAT, 'Empresa B não lê, altera nem exclui recursos da A por ID (IDOR)', 'todas 403/404', vazamentos or 'nenhum vazamento',
             not vazamentos, 'alta')

    listas = {rota: requisitar('GET', f'{base}/api/{rota}', token=emp_b.token, ip=ip_unico()).texto
              for rota in ('grupos-automovel', 'clientes', 'parceiros')}
    achou = [rota for rota, texto in listas.items() if any(x and x in texto for x in (gid, cid, pid))]
    r.checar('B12', CAT, 'As listagens da Empresa B não contêm nada da A', 'vazias de dados da A', achou or 'nada', not achou, 'alta')

    sobrou = requisitar('GET', f'{base}/api/clientes/{cid}', token=emp_a.token, ip=ip_unico()) if cid else None
    r.checar('B13', CAT, 'Depois dos ataques da B, o cliente da A continua intacto', '200', sobrou.status if sobrou else '-',
             bool(sobrou) and sobrou.ok and 'invadido' not in sobrou.texto, 'alta')

    # mass assignment: a empresa vem do token, nunca do corpo
    injetado = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp_a.token, ip=ip_unico(), json_corpo={
        'nome': 'Cliente injetado', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': '12345678901',
        'validadeCnh': (datetime.datetime.now() + datetime.timedelta(days=900)).isoformat(),
        'telefone': '(11) 99999-0000', 'email': 'inj@teste.local', 'endereco': 'Rua Exemplo C, 789, Centro', 'empresaId': emp_b.id, 'EmpresaId': emp_b.id})
    iid = _id(injetado)
    na_b = requisitar('GET', f'{base}/api/clientes', token=emp_b.token, ip=ip_unico()).texto
    r.checar('B14', CAT, 'Campo empresaId no corpo é ignorado (mass assignment): o registro não vai para outra empresa', 'fora da B',
             'visível na B' if iid and iid in na_b else 'ok', not (iid and iid in na_b), 'alta')

    # funcionário só enxerga a empresa dele
    f_lista = requisitar('GET', f'{base}/api/clientes', token=func.token, ip=ip_unico()).texto
    r.checar('B15', CAT, 'Funcionário da A enxerga os dados da A', 'contém o cliente da A', 'sim' if cid and cid in f_lista else 'não',
             bool(cid) and cid in f_lista, 'media')
    emp_b_cliente = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp_b.token, ip=ip_unico(), json_corpo={
        'nome': 'Cliente da B', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': '12345678901',
        'validadeCnh': (datetime.datetime.now() + datetime.timedelta(days=900)).isoformat(),
        'telefone': '(11) 99999-0000', 'email': 'cliente.b@teste.local', 'endereco': 'Rua Exemplo B, 456, Centro'})
    bid = _id(emp_b_cliente)
    f_lista = requisitar('GET', f'{base}/api/clientes', token=func.token, ip=ip_unico()).texto
    r.checar('B16', CAT, 'Funcionário da A não enxerga dados da B', 'não contém o cliente da B', 'vaza' if bid and bid in f_lista else 'ok',
             not (bid and bid in f_lista), 'alta')

    # --- B20-B22: auditoria -------------------------------------------------------------------
    aud_a = requisitar('GET', f'{base}/api/auditoria?limite=200', token=emp_a.token, ip=ip_unico())
    aud_b = requisitar('GET', f'{base}/api/auditoria?limite=200', token=emp_b.token, ip=ip_unico())
    r.checar('B20', CAT, 'A auditoria da A registra a criação do grupo', 'contém o id do grupo', aud_a.status,
             aud_a.ok and bool(gid) and gid in aud_a.texto, 'media', 'S14')
    r.checar('B21', CAT, 'A auditoria da B não contém nada da A', 'sem ids da A', 'vaza' if gid and gid in aud_b.texto else 'ok',
             not (gid and gid in aud_b.texto), 'alta', 'S14')
    dado = requisitar('GET', f'{base}/api/auditoria?limite=200', token=emp_a.token, ip=ip_unico()).texto
    r.checar('B22', CAT, 'A auditoria não guarda valores (nome do grupo/cliente) nem dados pessoais', 'sem valores',
             'valores presentes' if 'Grupo secreto' in dado or 'cliente.a@teste.local' in dado else 'ok',
             'Grupo secreto' not in dado and 'cliente.a@teste.local' not in dado, 'media', 'S14')

    # --- B23: eventos de autenticação na trilha (S14) -------------------------------------------------------
    ambiente.login(base, emp_a.email, 'Errada@123456')
    ambiente.login(base, emp_a.email, emp_a.senha)
    eventos = requisitar('GET', f'{base}/api/auditoria?limite=200', token=emp_a.token, ip=ip_unico()).texto
    r.checar('B23', CAT, 'Login e falha de login aparecem na trilha de auditoria da empresa', 'LoginOk e LoginFalhou',
             [e for e in ('LoginOk', 'LoginFalhou') if e in eventos], 'LoginOk' in eventos and 'LoginFalhou' in eventos, 'media', 'S14')
    eventos_b = requisitar('GET', f'{base}/api/auditoria?limite=200', token=emp_b.token, ip=ip_unico()).texto
    r.checar('B24', CAT, 'Os eventos de login da A não aparecem na trilha da B', 'sem eventos da A',
             'vaza' if emp_a.id in eventos_b else 'ok', emp_a.id not in eventos_b, 'alta', 'S14')

    # --- B30-B33: funcionário desligado (S26) ------------------------------------------------
    fid = _id(resp_f)
    excluir = requisitar('DELETE', f'{base}/api/funcionarios/{fid}', token=emp_a.token, ip=ip_unico())
    r.checar('B30', CAT, 'Empresa desliga o funcionário', '2xx', excluir.status, excluir.ok, 'media')
    ainda = requisitar('GET', f'{base}/api/clientes', token=func.token, ip=ip_unico())
    r.checar('B31', CAT, 'O access token do funcionário desligado deixa de valer na hora', '401/403', ainda.status,
             ainda.status in (401, 403), 'alta', 'S26')
    novo_login = ambiente.login(base, email_f, senha_f)
    r.checar('B32', CAT, 'Funcionário desligado não consegue entrar, com 4xx (nunca 500)', '400/401', novo_login.status,
             400 <= novo_login.status < 500, 'alta', 'S26')
    face = requisitar('POST', f'{base}/api/auth/entrar-facial', ip=ip_unico(), json_corpo={'email': email_f, 'imagemBase64': 'x'})
    r.checar('B33', CAT, 'Login facial do desligado também responde 4xx', '4xx', face.status, 400 <= face.status < 500, 'media', 'S26')

    # --- B40: troca de senha (S27) ------------------------------------------------------------
    nova = ambiente.senha_forte()
    ctx.lembrar(nova)
    troca = requisitar('POST', f'{base}/api/auth/alterar-senha', token=emp_a.token, ip=ip_unico(),
                       json_corpo={'senhaAtual': emp_a.senha, 'novaSenha': nova, 'confirmarNovaSenha': nova})
    r.checar('B40', CAT, 'Existe troca de senha autenticada (senha atual + nova)', '2xx', troca.status, troca.ok, 'alta', 'S27')
    if troca.ok:
        antiga = ambiente.login(base, emp_a.email, emp_a.senha)
        atual = ambiente.login(base, emp_a.email, nova)
        r.checar('B41', CAT, 'Depois da troca a senha antiga falha e a nova funciona', 'antiga 4xx / nova 200',
                 f'{antiga.status}/{atual.status}', 400 <= antiga.status < 500 and atual.ok, 'alta', 'S27')
        velho = requisitar('GET', f'{base}/api/dashboard/resumo', token=emp_a.token, ip=ip_unico())
        r.checar('B42', CAT, 'Depois da troca, o access token antigo é revogado', '401', velho.status, velho.status == 401, 'media', 'S27')
