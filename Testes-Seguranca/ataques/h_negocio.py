"""Regras de negócio sob ataque: reserva dupla do mesmo veículo (corrida), IDs de outra empresa dentro do corpo, períodos absurdos,
cupom de uso único usado várias vezes ao mesmo tempo e transições de estado do aluguel repetidas."""
import datetime
import random
import string
from concurrent.futures import ThreadPoolExecutor

from lib import ambiente
from lib.http import id_de as _id, ip_unico, requisitar

CAT = 'Regras de negócio'


def _agora():
    return datetime.datetime.now(datetime.timezone.utc)


def _iso(dt):
    return dt.isoformat()


def _cnh():
    return ''.join(random.choices(string.digits, k=11))


def _placa():
    letras = ''.join(random.choices(string.ascii_uppercase, k=3))
    return f'{letras}{random.randint(0, 9)}{random.choice(string.ascii_uppercase)}{random.randint(10, 99)}'


def _montar(base, conta, carros=1):
    """Grupo + plano de cobrança + automóveis + cliente + condutor da empresa. Devolve os ids ou None se algo falhar."""
    t = conta.token
    grupo = requisitar('POST', f'{base}/api/grupos-automovel', token=t, ip=ip_unico(), json_corpo={'nome': 'Grupo H ' + ''.join(random.choices(string.ascii_uppercase, k=6)), 'descricao': 'x'})
    gid = _id(grupo)
    if not gid:
        return None
    requisitar('POST', f'{base}/api/planos-cobranca', token=t, ip=ip_unico(), json_corpo={
        'grupoAutomovelId': gid, 'nome': 'Plano H', 'precoDiaria': 100, 'precoPorKm': 2, 'kmLivreLimite': 100})
    automoveis = []
    for _ in range(carros):
        a = requisitar('POST', f'{base}/api/automoveis', token=t, ip=ip_unico(), json_corpo={
            'placa': _placa(), 'marca': 'Marca', 'cor': 'Preto', 'modelo': 'Modelo', 'tipoCombustivel': 'Flex',
            'capacidadeTanque': 50, 'ano': 2022, 'foto': None, 'grupoAutomovelId': gid})
        if _id(a):
            automoveis.append(_id(a))
    validade = _iso(datetime.datetime.now() + datetime.timedelta(days=900))
    cliente = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=t, ip=ip_unico(), json_corpo={
        'nome': 'Cliente H', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': _cnh(), 'validadeCnh': validade,
        'telefone': '(11) 99999-0000', 'email': 'cliente.h@teste.local', 'endereco': 'Rua Exemplo H, 1, Centro'})
    condutor = requisitar('POST', f'{base}/api/condutores', token=t, ip=ip_unico(), json_corpo={
        'nome': 'Condutor H', 'email': 'condutor.h@teste.local', 'cpf': ambiente.cpf_valido(), 'cnh': _cnh(),
        'validadeCnh': validade, 'telefone': '(11) 98888-0000', 'clienteId': _id(cliente)})
    if not (automoveis and _id(cliente) and _id(condutor)):
        _montar.motivo = {'automoveis': len(automoveis), 'cliente': (cliente.status, cliente.texto[:90]), 'condutor': (condutor.status, condutor.texto[:90])}
        return None
    return {'grupo': gid, 'automoveis': automoveis, 'cliente': _id(cliente), 'condutor': _id(condutor)}


def _corpo_aluguel(d, automovel=None, dias_a_partir=2, duracao=3, **extra):
    saida = _agora() + datetime.timedelta(days=dias_a_partir)
    corpo = {'condutorId': d['condutor'], 'automovelId': automovel or d['automoveis'][0], 'clienteId': d['cliente'],
             'dataSaida': _iso(saida), 'dataRetornoPrevisto': _iso(saida + datetime.timedelta(days=duracao)),
             'valorPrevisto': 300, 'taxasServicosIds': []}
    corpo.update(extra)
    return corpo


def executar(ctx):
    r, base = ctx.reg, ctx.base
    a = ambiente.nova_empresa(base, 'negA')
    b = ambiente.nova_empresa(base, 'negB')
    ctx.lembrar(a.senha, a.token, b.senha, b.token)
    da = _montar(base, a, carros=6)
    db = _montar(base, b, carros=1)
    if not da or not db:
        r.info('H00', CAT, 'Cenário de negócio não pôde ser montado (cadastros recusados); ataques de negócio ignorados', bool(da) and bool(db), '')
        return

    # --- H01: reserva dupla (corrida entre a checagem de disponibilidade e a gravação) -------------------------------------
    corpo = _corpo_aluguel(da)
    with ThreadPoolExecutor(max_workers=10) as pool:
        resp = list(pool.map(lambda _: requisitar('POST', f'{base}/api/alugueis', token=a.token, ip=ip_unico(), json_corpo=corpo), range(10)))
    criados = [x for x in resp if x.status in (200, 201)]
    r.checar('H01', CAT, 'Corrida: 10 pedidos simultâneos para o MESMO carro e período criam no máximo um aluguel (sem reserva dupla)',
             '<= 1 aluguel criado', f'{len(criados)} criados; status {sorted(x.status for x in resp)}', len(criados) <= 1, 'alta')
    lista = requisitar('GET', f'{base}/api/alugueis', token=a.token, ip=ip_unico()).json()
    bruto = lista if isinstance(lista, list) else (lista or {})
    if isinstance(bruto, dict):
        for chave in ('registros', 'dados', 'itens'):
            interno = bruto.get(chave)
            if isinstance(interno, dict) and isinstance(interno.get('registros'), list):
                interno = interno['registros']
            if isinstance(interno, list):
                bruto = interno
                break
    itens = bruto if isinstance(bruto, list) else []
    do_carro = sum(1 for i in itens if isinstance(i, dict) and str(i.get('automovelId', '')).lower() == da['automoveis'][0].lower())
    if not itens:
        r.info('H01b', CAT, 'Listagem de alugueis não pôde ser lida para conferir o banco', type(lista).__name__, '')
    else:
        r.checar('H01b', CAT, 'Depois da corrida, o carro não tem dois aluguéis sobrepostos no banco', '<= 1 aluguel desse carro',
                 f'{do_carro} de {len(itens)} lidos', do_carro <= 1, 'alta')

    # --- H02: ids de OUTRA empresa dentro do corpo (IDOR por relação) -----------------------------------------------------------
    cruzados = {
        'carro da B na empresa A': _corpo_aluguel(da, automovel=db['automoveis'][0]),
        'cliente da B na empresa A': _corpo_aluguel(da, automovel=da['automoveis'][1], clienteId=db['cliente']),
        'condutor da B na empresa A': _corpo_aluguel(da, automovel=da['automoveis'][2], condutorId=db['condutor']),
    }
    aceitos = {}
    for nome, c in cruzados.items():
        x = requisitar('POST', f'{base}/api/alugueis', token=a.token, ip=ip_unico(), json_corpo=c)
        if x.status in (200, 201) or x.status >= 500:
            aceitos[nome] = x.status
    inverso = requisitar('POST', f'{base}/api/alugueis', token=b.token, ip=ip_unico(), json_corpo=_corpo_aluguel(da, automovel=da['automoveis'][3]))
    if inverso.status in (200, 201) or inverso.status >= 500:
        aceitos['carro/cliente/condutor da A na empresa B'] = inverso.status
    r.checar('H02', CAT, 'Aluguel não aceita carro, cliente ou condutor de outra empresa (nem 500)', '4xx em todos os cruzamentos',
             aceitos or '4xx', not aceitos, 'alta')

    # --- H03: períodos e valores absurdos -------------------------------------------------------------------------------------------
    agora = _agora()
    absurdos = {
        'retorno antes da saída': _corpo_aluguel(da, automovel=da['automoveis'][4], duracao=-2),
        'saída há 20 anos': _corpo_aluguel(da, automovel=da['automoveis'][4], dias_a_partir=-7300),
        'duração de 50 anos': _corpo_aluguel(da, automovel=da['automoveis'][4], duracao=18250),
        'datas iguais': _corpo_aluguel(da, automovel=da['automoveis'][4], duracao=0),
        'taxa de serviço inexistente': _corpo_aluguel(da, automovel=da['automoveis'][4],
                                                       taxasServicosIds=['00000000-0000-0000-0000-000000000001']),
    }
    ruins = {}
    for nome, c in absurdos.items():
        x = requisitar('POST', f'{base}/api/alugueis', token=a.token, ip=ip_unico(), json_corpo=c)
        if x.status in (200, 201) or x.status >= 500:
            ruins[nome] = x.status
    r.checar('H03', CAT, 'Períodos absurdos (volta antes de sair, no passado, 50 anos) e taxa de serviço inexistente são recusados com 4xx', '4xx em todos',
             ruins or '4xx', not ruins, 'media')

    # --- H04: o valor enviado pelo cliente não manda no preço -------------------------------------------------------------------------
    carro_preco = da['automoveis'][5]
    barato = requisitar('POST', f'{base}/api/alugueis', token=a.token, ip=ip_unico(), json_corpo=_corpo_aluguel(da, automovel=carro_preco, dias_a_partir=30, valorPrevisto=0.01))
    if barato.status in (200, 201):
        det = requisitar('GET', f'{base}/api/alugueis/{_id(barato)}', token=a.token, ip=ip_unico()).json() or {}
        dados = det.get('dados', det) if isinstance(det, dict) else {}
        valor = dados.get('valorPrevisto') if isinstance(dados, dict) else None
        r.checar('H04', CAT, 'Valor previsto enviado pelo cliente (R$ 0,01) é ignorado: o servidor calcula o preço', 'valor gravado > 1',
                 valor, isinstance(valor, (int, float)) and valor > 1, 'alta')
    else:
        r.info('H04', CAT, 'Aluguel de teste de preço não foi criado; verificação ignorada', barato.status, '')

    # --- H05: cupom de uso único usado por vários alugueis ao mesmo tempo -------------------------------------------------------------------
    codigo = 'UNICO' + ''.join(random.choices(string.ascii_uppercase, k=6))
    cupom = requisitar('POST', f'{base}/api/cupons', token=a.token, ip=ip_unico(), json_corpo={
        'codigo': codigo, 'descricao': 'Cupom de uso unico', 'tipoDesconto': 'Percentual', 'valorDesconto': 10,
        'validoAte': _iso(_agora() + datetime.timedelta(days=60)), 'limiteUsos': 1, 'parceiroId': None})
    if _id(cupom):
        db_extra = _montar(base, a, carros=6)
        if not db_extra:
            r.info('H05', CAT, 'Cenário do cupom não pôde ser montado; verificação ignorada', getattr(_montar, 'motivo', ''), '')
        else:
            def alugar(i):
                c = _corpo_aluguel(db_extra, automovel=db_extra['automoveis'][i], dias_a_partir=100 + i * 10, cupomCodigo=codigo)
                return requisitar('POST', f'{base}/api/alugueis', token=a.token, ip=ip_unico(), json_corpo=c).status
            with ThreadPoolExecutor(max_workers=6) as pool:
                status = list(pool.map(alugar, range(6)))
            usos = sum(1 for s in status if s in (200, 201))
            r.checar('H05', CAT, 'Cupom com limite de 1 uso, usado por 6 alugueis ao mesmo tempo, vale para no máximo um', '<= 1 aluguel com o cupom',
                     f'{usos} aceitos; {sorted(status)}', usos <= 1, 'media')
    else:
        r.info('H05', CAT, 'Cupom de teste não foi criado; verificação ignorada', cupom.status, '')

    # --- H06: transições de estado repetidas e simultâneas ------------------------------------------------------------------------------------
    carro_estado = requisitar('POST', f'{base}/api/automoveis', token=a.token, ip=ip_unico(), json_corpo={
        'placa': _placa(), 'marca': 'Marca', 'cor': 'Branco', 'modelo': 'Modelo', 'tipoCombustivel': 'Flex',
        'capacidadeTanque': 50, 'ano': 2022, 'foto': None, 'grupoAutomovelId': da['grupo']})
    if _id(carro_estado):
        ag = requisitar('POST', f'{base}/api/alugueis', token=a.token, ip=ip_unico(), json_corpo=_corpo_aluguel(da, automovel=_id(carro_estado), dias_a_partir=200))
        if _id(ag):
            aid = _id(ag)
            with ThreadPoolExecutor(max_workers=6) as pool:
                statuses = list(pool.map(lambda _: requisitar('POST', f'{base}/api/alugueis/{aid}/iniciar', token=a.token, ip=ip_unico(),
                                                              json_corpo={'id': aid}).status, range(6)))
            sucessos = sum(1 for s in statuses if 200 <= s < 300)
            apos = requisitar('POST', f'{base}/api/alugueis/{aid}/cancelar', token=a.token, ip=ip_unico(), json_corpo={'id': aid})
            excluir = requisitar('DELETE', f'{base}/api/alugueis/{aid}', token=a.token, ip=ip_unico())
            r.checar('H06', CAT, 'Iniciar o mesmo aluguel 6 vezes ao mesmo tempo funciona no máximo uma vez; nenhuma transição responde 5xx',
                     '<= 1 sucesso e sem 5xx', f'{sucessos} sucessos; cancelar {apos.status}; excluir {excluir.status}',
                     sucessos <= 1 and max(statuses + [apos.status, excluir.status]) < 500, 'media')
