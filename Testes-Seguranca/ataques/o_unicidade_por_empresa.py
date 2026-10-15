"""Unicidade POR EMPRESA: o mesmo CPF, CNPJ, CNH, placa, nome ou código cadastrado por duas empresas diferentes tem de ser aceito nas duas, e a segunda
nunca descobre (por um 409) que a primeira já tinha aquele dado. Se o índice único do banco fosse global (sem EmpresaId), uma locadora não conseguiria
cadastrar um cliente que outra já cadastrou, e o 409 viraria um oráculo: dá para descobrir quais CPFs existem nas outras empresas."""
import datetime
import random
import string

from lib import ambiente
from lib.http import id_de as _id, ip_unico, requisitar

from .h_negocio import _cnh, _iso, _placa

CAT = 'Isolamento'


def executar(ctx):
    r, base = ctx.reg, ctx.base
    a = ambiente.nova_empresa(base, 'uniA')
    b = ambiente.nova_empresa(base, 'uniB')
    ctx.lembrar(a.senha, a.token, b.senha, b.token)
    validade = _iso(datetime.datetime.now() + datetime.timedelta(days=900))
    sufixo = ''.join(random.choices(string.ascii_uppercase, k=5))
    cpf, cnpj, cnh, placa = ambiente.cpf_valido(), ambiente.cnpj_valido(), _cnh(), _placa()
    cpf_condutor, cpf_func = ambiente.cpf_valido(), ambiente.cpf_valido()

    def post(conta, caminho, corpo):
        return requisitar('POST', f'{base}{caminho}', token=conta.token, ip=ip_unico(), json_corpo=corpo)

    def em_cada_empresa(caminho, corpo_de):
        """Cadastra o MESMO registro nas duas empresas e devolve (resposta de A, resposta de B)."""
        return post(a, caminho, corpo_de(a)), post(b, caminho, corpo_de(b))

    casos = {}
    casos['cliente pessoa física (CPF)'] = em_cada_empresa('/api/clientes/pessoa-fisica', lambda c: {
        'nome': 'Cliente Isolamento', 'cpf': cpf, 'rg': '123456789', 'cnh': cnh, 'validadeCnh': validade,
        'telefone': '(11) 99999-0000', 'email': 'iso@teste.local', 'endereco': 'Rua Exemplo, 1, Centro'})
    casos['empresa-cliente (CNPJ)'] = em_cada_empresa('/api/clientes/pessoa-juridica', lambda c: {
        'nome': 'Empresa Isolamento', 'cnpj': cnpj, 'nomeFantasia': 'Fantasia Isolamento', 'telefone': '(11) 3333-4444',
        'email': 'iso-pj@teste.local', 'endereco': 'Av. Exemplo, 2, Centro'})
    casos['parceiro (CNPJ)'] = em_cada_empresa('/api/parceiros', lambda c: {'nome': 'Parceiro Isolamento', 'cnpj': cnpj, 'categoria': 'Hotel'})
    casos['grupo (nome)'] = em_cada_empresa('/api/grupos-automovel', lambda c: {'nome': 'Grupo ' + sufixo, 'descricao': 'x'})
    casos['taxa (nome)'] = em_cada_empresa('/api/taxas-servicos', lambda c: {'nome': 'Taxa ' + sufixo, 'preco': 10, 'tipoCalculo': 'Fixo'})
    casos['cupom (código)'] = em_cada_empresa('/api/cupons', lambda c: {
        'codigo': 'ISO' + sufixo, 'descricao': 'iso', 'tipoDesconto': 'Percentual', 'valorDesconto': 10,
        'validoAte': _iso(datetime.datetime.now() + datetime.timedelta(days=30)), 'limiteUsos': 3, 'parceiroId': None})
    casos['funcionário (CPF)'] = em_cada_empresa('/api/funcionarios', lambda c: _funcionario(ctx, cpf_func, c))

    # veículo e condutor dependem de registros da própria empresa
    grupos = {}
    for conta in (a, b):
        grupos[conta.email] = _id(post(conta, '/api/grupos-automovel', {'nome': 'Grupo veiculo ' + sufixo, 'descricao': 'x'}))
    placas = (
        post(a, '/api/automoveis', {'placa': placa, 'marca': 'Marca', 'cor': 'Preto', 'modelo': 'Modelo', 'tipoCombustivel': 'Flex',
                                    'capacidadeTanque': 50, 'ano': 2022, 'foto': None, 'grupoAutomovelId': grupos[a.email]}),
        post(b, '/api/automoveis', {'placa': placa, 'marca': 'Marca', 'cor': 'Preto', 'modelo': 'Modelo', 'tipoCombustivel': 'Flex',
                                    'capacidadeTanque': 50, 'ano': 2022, 'foto': None, 'grupoAutomovelId': grupos[b.email]}))
    casos['automóvel (placa)'] = placas

    clientes = {}
    for conta in (a, b):
        clientes[conta.email] = _id(post(conta, '/api/clientes/pessoa-fisica', {
            'nome': 'Dono do condutor', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': _cnh(), 'validadeCnh': validade,
            'telefone': '(11) 99999-0000', 'email': 'dono@teste.local', 'endereco': 'Rua Exemplo, 1, Centro'}))
    casos['condutor (CPF e CNH)'] = (
        post(a, '/api/condutores', {'nome': 'Condutor Isolamento', 'email': 'c@teste.local', 'cpf': cpf_condutor, 'cnh': cnh, 'validadeCnh': validade,
                                    'telefone': '(11) 98888-0000', 'clienteId': clientes[a.email]}),
        post(b, '/api/condutores', {'nome': 'Condutor Isolamento', 'email': 'c@teste.local', 'cpf': cpf_condutor, 'cnh': cnh, 'validadeCnh': validade,
                                    'telefone': '(11) 98888-0000', 'clienteId': clientes[b.email]}))

    recusados = {nome: (ra.status, rb.status) for nome, (ra, rb) in casos.items() if not (ra.status in (200, 201) and rb.status in (200, 201))}
    r.checar('O01', CAT, 'O mesmo CPF, CNPJ, CNH, placa, nome ou código cadastrado por DUAS empresas é aceito nas duas (a unicidade é por empresa)',
             'todos 201 nas duas', recusados or 'todos aceitos', not recusados, 'alta', 'S45')
    oraculo = [nome for nome, (ra, rb) in casos.items() if rb.status == 409]
    r.checar('O02', CAT, 'A segunda empresa nunca recebe 409 por um dado que só a primeira cadastrou (sem oráculo de cadastros de outras empresas)',
             'nenhum 409 cruzado', oraculo or 'nenhum', not oraculo, 'alta', 'S45')


def _funcionario(ctx, cpf, conta):
    senha = ambiente.senha_forte()
    ctx.lembrar(senha)
    return {'nomeCompleto': 'Func Isolamento', 'cpf': cpf, 'email': f'iso-{ambiente.secrets.token_hex(3)}@teste.local', 'senha': senha,
            'confirmarSenha': senha, 'salario': 3000, 'admissaoEmUtc': _iso(datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=10))}
