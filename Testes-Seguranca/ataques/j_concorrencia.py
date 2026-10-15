"""Corridas (requisições simultâneas): o mesmo cadastro enviado várias vezes AO MESMO TEMPO (CPF, CNPJ, CNH, placa, nome, e-mail),
devolução repetida e exclusão que disputa com criação. A checagem "já existe?" e a gravação são dois passos; se dois pedidos
passam juntos pela checagem, só o índice único do banco impede o duplicado, e o erro dele precisa virar 409, nunca 500.
Cada verificação confere o RESULTADO NO BANCO (quantos registros existem de verdade), não só o status das respostas."""
import datetime
import random
import secrets
import string
from concurrent.futures import ThreadPoolExecutor

from lib import ambiente
from lib.http import id_de as _id, ip_unico, requisitar

from .h_negocio import _agora, _cnh, _corpo_aluguel, _iso, _montar, _placa

CAT = 'Corridas'
RAJADA = 10


def _rajada(n, fn):
    with ThreadPoolExecutor(max_workers=n) as pool:
        return list(pool.map(fn, range(n)))


def _registros(resp):
    j = resp.json()
    if isinstance(j, dict):
        j = j.get('registros', j.get('dados', []))
    return j if isinstance(j, list) else []


def _sufixo(n=6):
    return ''.join(random.choices(string.ascii_uppercase, k=n))


def _um_so(r, id_, titulo, respostas, contar_no_banco, ref=''):
    """Passa quando EXATAMENTE um pedido foi aceito, nenhum virou 5xx e o banco guardou um único registro."""
    status = sorted(x.status for x in respostas)
    aceitos = sum(1 for s in status if s in (200, 201))
    cinco_xx = [s for s in status if s >= 500 or s == 0]
    no_banco = contar_no_banco()
    r.checar(id_, CAT, titulo, 'exatamente 1 aceito, sem 5xx, 1 no banco',
             f'{aceitos} aceitos; status {status}; {no_banco} no banco', aceitos == 1 and not cinco_xx and no_banco == 1,
             'alta' if aceitos > 1 or no_banco > 1 else 'media', ref)


def executar(ctx):
    r, base = ctx.reg, ctx.base
    emp = ambiente.nova_empresa(base, 'corrida')
    ctx.lembrar(emp.senha, emp.token)
    t = emp.token

    def post(caminho, corpo):
        return requisitar('POST', f'{base}{caminho}', token=t, ip=ip_unico(), json_corpo=corpo)

    def contar(caminho, campo, valor, prefixo=False):
        itens = _registros(requisitar('GET', f'{base}{caminho}', token=t, ip=ip_unico()))
        limpo = lambda x: str(x).replace('.', '').replace('-', '').replace('/', '')
        alvo = limpo(valor)
        return sum(1 for i in itens if isinstance(i, dict) and (limpo(i.get(campo, '')).startswith(alvo) if prefixo else limpo(i.get(campo, '')) == alvo))

    validade = _iso(datetime.datetime.now() + datetime.timedelta(days=900))

    # --- J01/J02: o mesmo CPF / CNPJ de cliente enviado 10 vezes ao mesmo tempo -----------------------------------------------------
    cpf = ambiente.cpf_valido()
    resp = _rajada(RAJADA, lambda i: post('/api/clientes/pessoa-fisica', {
        'nome': f'Cliente {i}', 'cpf': cpf, 'rg': '123456789', 'cnh': _cnh(), 'validadeCnh': validade, 'telefone': '(11) 99999-0000',
        'email': f'c{i}@teste.local', 'endereco': 'Rua Exemplo, 1, Centro'}))
    _um_so(r, 'J01', 'Corrida: 10 cadastros de cliente com o MESMO CPF criam um só', resp,
           lambda: contar('/api/clientes/pessoas-fisicas', 'cpf', cpf.replace('.', '').replace('-', '')), 'S35')

    cnpj = ambiente.cnpj_valido()
    resp = _rajada(RAJADA, lambda i: post('/api/clientes/pessoa-juridica', {
        'nome': f'Empresa {i}', 'cnpj': cnpj, 'nomeFantasia': 'Fantasia', 'telefone': '(11) 3333-4444', 'email': f'pj{i}@teste.local',
        'endereco': 'Av. Exemplo, 2, Centro'}))
    _um_so(r, 'J02', 'Corrida: 10 cadastros de empresa-cliente com o MESMO CNPJ criam uma só', resp,
           lambda: contar('/api/clientes/pessoas-juridicas', 'cnpj', cnpj.replace('.', '').replace('-', '').replace('/', '')), 'S35')

    # --- J03/J04: condutor com o mesmo CPF e com a mesma CNH ----------------------------------------------------------------------
    dono = _id(post('/api/clientes/pessoa-fisica', {
        'nome': 'Dono dos condutores', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': _cnh(), 'validadeCnh': validade,
        'telefone': '(11) 99999-0000', 'email': 'dono@teste.local', 'endereco': 'Rua Exemplo, 1, Centro'}))
    if dono:
        def condutor(cpf_, cnh_, i):
            return post('/api/condutores', {'nome': f'Condutor {i}', 'email': f'k{i}@teste.local', 'cpf': cpf_, 'cnh': cnh_,
                                            'validadeCnh': validade, 'telefone': '(11) 98888-0000', 'clienteId': dono})
        cpf_c = ambiente.cpf_valido()
        resp = _rajada(RAJADA, lambda i: condutor(cpf_c, _cnh(), i))
        _um_so(r, 'J03', 'Corrida: 10 condutores com o MESMO CPF criam um só', resp,
               lambda: contar('/api/condutores', 'cpf', cpf_c.replace('.', '').replace('-', '')), 'S35')
        cnh_c = _cnh()
        resp = _rajada(RAJADA, lambda i: condutor(ambiente.cpf_valido(), cnh_c, i))
        _um_so(r, 'J04', 'Corrida: 10 condutores com a MESMA CNH criam um só', resp, lambda: contar('/api/condutores', 'cnh', cnh_c), 'S35')
    else:
        r.info('J03', CAT, 'Cliente-dono dos condutores não foi criado; J03/J04 ignoradas', '', '')

    # --- J05..J09: placa, nome de grupo, código de cupom, nome de taxa e CNPJ de parceiro --------------------------------------------
    grupo = _id(post('/api/grupos-automovel', {'nome': 'Grupo corrida ' + _sufixo(), 'descricao': 'x'}))
    placa = _placa()
    if grupo:
        resp = _rajada(RAJADA, lambda i: post('/api/automoveis', {
            'placa': placa, 'marca': 'Marca', 'cor': 'Preto', 'modelo': f'Modelo {i}', 'tipoCombustivel': 'Flex',
            'capacidadeTanque': 50, 'ano': 2022, 'foto': None, 'grupoAutomovelId': grupo}))
        _um_so(r, 'J05', 'Corrida: 10 cadastros de automóvel com a MESMA placa criam um só', resp, lambda: contar('/api/automoveis', 'placa', placa), 'S35')

    nome_grupo = 'Grupo duplicado ' + _sufixo()
    resp = _rajada(RAJADA, lambda i: post('/api/grupos-automovel', {'nome': nome_grupo, 'descricao': f'd{i}'}))
    _um_so(r, 'J06', 'Corrida: 10 grupos com o MESMO nome criam um só', resp, lambda: contar('/api/grupos-automovel', 'nome', nome_grupo), 'S35')

    codigo = 'DUP' + _sufixo()
    resp = _rajada(RAJADA, lambda i: post('/api/cupons', {
        'codigo': codigo, 'descricao': f'cupom {i}', 'tipoDesconto': 'Percentual', 'valorDesconto': 10,
        'validoAte': _iso(_agora() + datetime.timedelta(days=60)), 'limiteUsos': 5, 'parceiroId': None}))
    _um_so(r, 'J07', 'Corrida: 10 cupons com o MESMO código criam um só', resp, lambda: contar('/api/cupons', 'codigo', codigo), 'S35')

    nome_taxa = 'Taxa duplicada ' + _sufixo()
    resp = _rajada(RAJADA, lambda i: post('/api/taxas-servicos', {'nome': nome_taxa, 'preco': 10 + i, 'tipoCalculo': 'Fixo'}))
    _um_so(r, 'J08', 'Corrida: 10 taxas com o MESMO nome criam uma só', resp, lambda: contar('/api/taxas-servicos', 'nome', nome_taxa), 'S35')

    cnpj_p = ambiente.cnpj_valido()
    resp = _rajada(RAJADA, lambda i: post('/api/parceiros', {'nome': f'Parceiro {i}', 'cnpj': cnpj_p, 'categoria': 'Hotel'}))
    _um_so(r, 'J09', 'Corrida: 10 parceiros com o MESMO CNPJ criam um só', resp,
           lambda: contar('/api/parceiros', 'cnpj', cnpj_p.replace('.', '').replace('-', '').replace('/', '')), 'S35')

    # --- J10/J11: funcionário com o mesmo CPF e com o mesmo e-mail ------------------------------------------------------------------
    def funcionario(cpf_, email, i, marca):
        senha = ambiente.senha_forte()
        ctx.lembrar(senha)
        return post('/api/funcionarios', {'nomeCompleto': f'{marca}{i}', 'cpf': cpf_, 'email': email, 'senha': senha, 'confirmarSenha': senha,
                                          'salario': 3000, 'admissaoEmUtc': _iso(_agora() - datetime.timedelta(days=30))})
    cpf_f = ambiente.cpf_valido()
    marca_cpf = 'MesmoCpf' + _sufixo(4) + '-'
    resp = _rajada(RAJADA, lambda i: funcionario(cpf_f, f'f{i}-{secrets.token_hex(3)}@teste.local', i, marca_cpf))
    _um_so(r, 'J10', 'Corrida: 10 funcionários com o MESMO CPF criam um só', resp,
           lambda: contar('/api/funcionarios', 'nomeCompleto', marca_cpf, prefixo=True), 'S35')
    email_f = f'igual-{secrets.token_hex(4)}@teste.local'
    resp = _rajada(RAJADA, lambda i: funcionario(ambiente.cpf_valido(), email_f, i, 'MesmoEmail'))
    _um_so(r, 'J11', 'Corrida: 10 funcionários com o MESMO e-mail criam um só', resp, lambda: contar('/api/funcionarios', 'email', email_f), 'S35')

    # --- J12: cadastro de empresa (conta) com o mesmo e-mail ao mesmo tempo ---------------------------------------------------------
    email_e = f'conta-{secrets.token_hex(4)}@teste.local'
    senha_e = ambiente.senha_forte()
    ctx.lembrar(senha_e)
    contas = _rajada(RAJADA, lambda i: requisitar('POST', f'{base}/api/auth/registrar', ip=ip_unico(), json_corpo={
        'nomeCompleto': f'Conta {i}', 'email': email_e, 'senha': senha_e, 'confirmarSenha': senha_e}))
    aceitas = sum(1 for x in contas if x.ok)
    r.checar('J12', CAT, 'Corrida: 10 cadastros de conta com o MESMO e-mail criam uma só (os demais recusados, sem 5xx)', '1 aceito e sem 5xx',
             f'{aceitas} aceitos; {sorted(x.status for x in contas)}', aceitas == 1 and all(0 < x.status < 500 for x in contas), 'alta', 'S35')
    if aceitas == 1:
        entrar = requisitar('POST', f'{base}/api/auth/autenticar', ip=ip_unico(), json_corpo={'email': email_e, 'senha': senha_e})
        r.checar('J12b', CAT, 'A conta que sobrou entra com a senha cadastrada (os pedidos perdedores não corromperam nada)', '200', entrar.status,
                 entrar.status == 200, 'media', 'S35')

    # --- J13/J14: devolução e transições de estado em corrida -----------------------------------------------------------------------
    d = _montar(base, emp, carros=3)
    if not d:
        r.info('J13', CAT, 'Cenário de aluguel não pôde ser montado; J13..J15 ignoradas', getattr(_montar, 'motivo', ''), '')
        return
    carro_a, carro_b, carro_c = d['automoveis']

    def aluguel_em_andamento(carro):
        # saída daqui a pouco e iniciado: a devolução só vale para aluguel em andamento
        saida = _agora() + datetime.timedelta(seconds=5)
        corpo = _corpo_aluguel(d, automovel=carro, dias_a_partir=0, duracao=2)
        corpo['dataSaida'] = _iso(saida)
        corpo['dataRetornoPrevisto'] = _iso(saida + datetime.timedelta(days=2))
        criado = _id(post('/api/alugueis', corpo))
        if criado:
            requisitar('POST', f'{base}/api/alugueis/{criado}/iniciar', token=t, ip=ip_unico(), json_corpo={'id': criado})
        return criado

    aluguel = aluguel_em_andamento(carro_a)
    if aluguel:
        corpo_dev = {'aluguelId': aluguel, 'dataDevolucao': _iso(_agora() + datetime.timedelta(days=2)), 'quilometragemFinal': 15000,
                     'combustivelNoTanque': 50, 'nivelCombustivel': 'Cheio'}
        resp = _rajada(6, lambda i: post('/api/devolucoes', corpo_dev))
        placa_a = (requisitar('GET', f'{base}/api/automoveis/{carro_a}', token=t, ip=ip_unico()).json() or {}).get('placa')
        lista = _registros(requisitar('GET', f'{base}/api/devolucoes', token=t, ip=ip_unico()))
        da_locacao = sum(1 for x in lista if isinstance(x, dict) and x.get('automovelPlaca') == placa_a)
        status = sorted(x.status for x in resp)
        aceitos = sum(1 for s in status if s in (200, 201))
        r.checar('J13', CAT, 'Corrida: 6 devoluções simultâneas do MESMO aluguel registram uma só (sem 5xx)', 'exatamente 1 aceita e 1 no banco',
                 f'{aceitos} aceitas; status {status}; {da_locacao} no banco', aceitos == 1 and da_locacao == 1 and max(status) < 500, 'alta')
    else:
        r.info('J13', CAT, 'Aluguel em andamento para a devolução não pôde ser criado; J13 ignorada', '', '')

    # iniciar e cancelar ao mesmo tempo: o aluguel termina num estado só, coerente com a resposta que deu certo
    aluguel2 = _id(post('/api/alugueis', _corpo_aluguel(d, automovel=carro_b, dias_a_partir=60)))
    if aluguel2:
        def acao(i):
            caminho = 'iniciar' if i % 2 == 0 else 'cancelar'
            return caminho, requisitar('POST', f'{base}/api/alugueis/{aluguel2}/{caminho}', token=t, ip=ip_unico(), json_corpo={'id': aluguel2})
        resultados = _rajada(8, acao)
        estado = (requisitar('GET', f'{base}/api/alugueis/{aluguel2}', token=t, ip=ip_unico()).json() or {})
        estado = estado.get('status') if isinstance(estado, dict) else None
        sucessos = {c for c, x in resultados if x.ok}
        coerente = (len(sucessos) <= 1) or (str(estado) in ('Cancelado', 'EmAndamento'))
        r.checar('J14', CAT, 'Corrida: iniciar e cancelar o mesmo aluguel ao mesmo tempo termina em UM estado, sem 5xx',
                 'sem 5xx e um estado final válido', f'estado {estado}; sucessos {sorted(sucessos)}; status {sorted(x.status for _, x in resultados)}',
                 max(x.status for _, x in resultados) < 500 and coerente and estado in ('Cancelado', 'EmAndamento'), 'media')

    # --- J15: excluir o cliente enquanto um aluguel dele está sendo criado ----------------------------------------------------------
    orfaos = []
    for _ in range(4):
        cli = _id(post('/api/clientes/pessoa-fisica', {
            'nome': 'Cliente da corrida', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': _cnh(), 'validadeCnh': validade,
            'telefone': '(11) 99999-0000', 'email': 'cc@teste.local', 'endereco': 'Rua Exemplo, 1, Centro'}))
        cond = _id(post('/api/condutores', {'nome': 'Condutor da corrida', 'email': 'cd@teste.local', 'cpf': ambiente.cpf_valido(), 'cnh': _cnh(),
                                            'validadeCnh': validade, 'telefone': '(11) 98888-0000', 'clienteId': cli})) if cli else None
        if not (cli and cond):
            continue
        dd = dict(d, cliente=cli, condutor=cond)

        def alugar_ou_excluir(i, cli=cli, dd=dd):
            if i == 0:
                return 'excluir', requisitar('DELETE', f'{base}/api/clientes/{cli}', token=t, ip=ip_unico())
            return 'alugar', requisitar('POST', f'{base}/api/alugueis', token=t, ip=ip_unico(),
                                        json_corpo=_corpo_aluguel(dd, automovel=carro_c, dias_a_partir=300 + random.randint(0, 5000)))
        res = _rajada(2, alugar_ou_excluir)
        excluiu = any(c == 'excluir' and x.ok for c, x in res)
        alugou = any(c == 'alugar' and x.ok for c, x in res)
        if excluiu and alugou:
            orfaos.append(cli)
        if any(x.status >= 500 for _, x in res):
            orfaos.append(f'5xx em {cli}')
    r.checar('J15', CAT, 'Corrida: excluir o cliente e criar um aluguel dele ao mesmo tempo não deixa aluguel órfão nem 5xx', 'nenhum órfão',
             orfaos or 'nenhum', not orfaos, 'media')
