"""Dados de exemplo para as telas terem o que mostrar nas medições (Lighthouse e ponta a ponta).

Tudo é criado pela própria API, com uma empresa nova e e-mails @teste.local, no banco de métricas (separado do banco de uso).
Aluguéis nos três estados que as telas mostram: reservado, em andamento e concluído (com devolução)."""
import datetime
import random
import string

from lib import ambiente  # Testes-Seguranca/lib (o orquestrador põe a pasta no sys.path)
from lib.http import id_de, ip_unico, requisitar


def _iso(dt):
    return dt.isoformat()


def _agora():
    return datetime.datetime.now(datetime.timezone.utc)


def _cnh():
    return ''.join(random.choices(string.digits, k=11))


def _placa():
    letras = ''.join(random.choices(string.ascii_uppercase, k=3))
    return f'{letras}{random.randint(0, 9)}{random.choice(string.ascii_uppercase)}{random.randint(10, 99)}'


NOMES = ['Ana Souza', 'Bruno Lima', 'Carla Dias', 'Diego Rocha', 'Elisa Prado', 'Fabio Nunes', 'Gabi Torres', 'Hugo Matos']


def _post(base, token, rota, corpo):
    return requisitar('POST', f'{base}{rota}', token=token, ip=ip_unico(), json_corpo=corpo)


def semear(base: str, conta, alugueis_reservados=3, alugueis_em_andamento=3, alugueis_concluidos=4) -> dict:
    """Devolve um resumo do que foi criado (contagens e ids úteis para os testes). Falhas viram erro explícito."""
    t = conta.token
    validade = _iso(datetime.datetime.now() + datetime.timedelta(days=900))

    grupos = []
    for nome in ('Econômico', 'SUV', 'Executivo'):
        g = id_de(_post(base, t, '/api/grupos-automovel', {'nome': nome}))
        if g:
            grupos.append(g)
            _post(base, t, '/api/planos-cobranca', {'grupoAutomovelId': g, 'nome': f'Plano {nome}', 'precoDiaria': 100,
                                                   'precoPorKm': 2, 'kmLivreLimite': 100})
    if not grupos:
        raise RuntimeError('semeadura: não foi possível criar grupos de automóvel')

    total = alugueis_reservados + alugueis_em_andamento + alugueis_concluidos
    automoveis = []
    for i in range(total + 3):
        a = _post(base, t, '/api/automoveis', {'placa': _placa(), 'marca': random.choice(['Fiat', 'VW', 'Chevrolet', 'Toyota']),
                                              'cor': 'Prata', 'modelo': f'Modelo {i + 1}', 'tipoCombustivel': 'Flex',
                                              'capacidadeTanque': 50, 'ano': 2022, 'foto': None,
                                              'grupoAutomovelId': grupos[i % len(grupos)]})
        if id_de(a):
            automoveis.append(id_de(a))

    clientes, condutores = [], []
    for i, nome in enumerate(NOMES):
        c = _post(base, t, '/api/clientes/pessoa-fisica', {
            'nome': nome, 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': _cnh(), 'validadeCnh': validade,
            'telefone': '(11) 99999-0000', 'email': f'cliente{i}@teste.local', 'endereco': f'Rua Exemplo, {i + 1}, Centro'})
        cid = id_de(c)
        if not cid:
            continue
        clientes.append(cid)
        d = _post(base, t, '/api/condutores', {
            'nome': f'Condutor {nome}', 'email': f'condutor{i}@teste.local', 'cpf': ambiente.cpf_valido(), 'cnh': _cnh(),
            'validadeCnh': validade, 'telefone': '(11) 98888-0000', 'clienteId': cid})
        if id_de(d):
            condutores.append((cid, id_de(d)))

    if not (automoveis and condutores):
        raise RuntimeError(f'semeadura: faltaram dados (automóveis={len(automoveis)}, condutores={len(condutores)})')

    def corpo_aluguel(i, dias_a_partir):
        cliente, condutor = condutores[i % len(condutores)]
        saida = _agora() + datetime.timedelta(days=dias_a_partir)
        return {'condutorId': condutor, 'automovelId': automoveis[i], 'clienteId': cliente, 'dataSaida': _iso(saida),
                'dataRetornoPrevisto': _iso(saida + datetime.timedelta(days=3)), 'valorPrevisto': 300, 'taxasServicosIds': []}

    ids = {'reservados': [], 'em_andamento': [], 'concluidos': []}
    indice = 0
    for _ in range(alugueis_reservados):
        a = id_de(_post(base, t, '/api/alugueis', corpo_aluguel(indice, 5 + indice)))
        if a:
            ids['reservados'].append(a)
        indice += 1
    for _ in range(alugueis_em_andamento):
        a = id_de(_post(base, t, '/api/alugueis', corpo_aluguel(indice, 2 + indice)))
        if a and _post(base, t, f'/api/alugueis/{a}/iniciar', {}).ok:
            ids['em_andamento'].append(a)
        indice += 1
    for _ in range(alugueis_concluidos):
        a = id_de(_post(base, t, '/api/alugueis', corpo_aluguel(indice, 2 + indice)))
        if a and _post(base, t, f'/api/alugueis/{a}/iniciar', {}).ok:
            dev = _post(base, t, '/api/devolucoes', {'aluguelId': a, 'dataDevolucao': _iso(_agora() + datetime.timedelta(days=indice + 6)),
                                                     'quilometragemFinal': 12000 + indice * 150, 'combustivelNoTanque': 40,
                                                     'nivelCombustivel': 'Cheio'})
            if dev.ok:
                ids['concluidos'].append(a)
        indice += 1

    return {'grupos': len(grupos), 'automoveis': len(automoveis), 'clientes': len(clientes), 'condutores': len(condutores),
            'alugueis': {k: len(v) for k, v in ids.items()}, 'ids': ids}
