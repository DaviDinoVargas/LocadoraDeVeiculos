"""Duas instâncias da Web API ao mesmo tempo, no mesmo banco (como num ambiente com balanceador): as travas em memória (fila por empresa, anti-replay
do TOTP, limitador de login) existem em UM processo só. Este módulo manda os pedidos de uma corrida metade para cada instância e confere o
RESULTADO NO BANCO. O que reprovar aqui é limite real da arquitetura de instância única, não um erro do teste: fica registrado com o motivo."""
import datetime
import secrets
from concurrent.futures import ThreadPoolExecutor

from lib import ambiente
from lib.http import id_de as _id, ip_unico, requisitar

from .h_negocio import _agora, _corpo_aluguel, _iso, _montar
from .j_concorrencia import _registros

CAT = 'Duas instâncias'
PORTA_B = 5184


def _rajada_alternada(n, fn):
    with ThreadPoolExecutor(max_workers=n) as pool:
        return list(pool.map(fn, range(n)))


def executar(ctx):
    r = ctx.reg
    if not ctx.pasta:
        return
    api_b = ambiente.iniciar_api(ctx.pasta, PORTA_B, nome='api-b')
    ctx.servicos_extras.append(api_b)
    try:
        api_b.iniciar()
    except SystemExit as ex:
        api_b.parar()
        r.info('N00', CAT, 'A segunda instância não subiu; módulo ignorado', str(ex)[:160], '')
        return
    try:
        _corridas(ctx, ctx.base, f'http://127.0.0.1:{PORTA_B}')
    finally:
        api_b.parar()


def _corridas(ctx, base_a, base_b):
    r = ctx.reg
    emp = ambiente.nova_empresa(base_a, 'duas')
    ctx.lembrar(emp.senha, emp.token)
    t = emp.token

    def onde(i):
        return base_a if i % 2 == 0 else base_b

    def post(i, caminho, corpo):
        return requisitar('POST', f'{onde(i)}{caminho}', token=t, ip=ip_unico(), json_corpo=corpo)

    controle = requisitar('GET', f'{base_b}/api/auth/sessao', token=t, ip=ip_unico())
    r.checar('N00', CAT, 'O mesmo token vale nas duas instâncias (mesma chave e mesmo banco)', '200', controle.status, controle.status == 200, 'baixa')

    d = _montar(base_a, emp, carros=12)
    if not d:
        r.info('N01', CAT, 'Cenário não pôde ser montado; módulo ignorado', getattr(_montar, 'motivo', ''), '')
        return

    # --- N01: reserva dupla do mesmo carro, pedidos divididos entre as instâncias (várias rodadas: a janela da corrida é pequena) ----------------------------
    rodadas, falhas, status_n01 = 12, [], []
    for k in range(rodadas):
        corpo = _corpo_aluguel(d, automovel=d['automoveis'][k % 12], dias_a_partir=300 + k * 10)
        resp = _rajada_alternada(12, lambda i: post(i, '/api/alugueis', corpo))
        criados = sum(1 for x in resp if x.status in (200, 201))
        status_n01.extend(x.status for x in resp)
        if criados > 1:
            falhas.append(f'rodada {k}: {criados} criadas')
    r.checar('N01', CAT, f'{rodadas} rodadas de 12 reservas do MESMO carro e período, metade em cada instância: nunca mais de uma criada', 'sempre <= 1 criada',
             falhas or f'{rodadas} rodadas sem reserva dupla', not falhas and max(status_n01) < 500, 'alta')

    # --- N02: cupom de uso único usado nas duas instâncias ------------------------------------------------------------------------------
    codigo = 'DUAS' + secrets.token_hex(3).upper()
    post(0, '/api/cupons', {'codigo': codigo, 'descricao': 'uso único', 'tipoDesconto': 'Percentual', 'valorDesconto': 10,
                            'validoAte': _iso(_agora() + datetime.timedelta(days=60)), 'limiteUsos': 1, 'parceiroId': None})
    resp = _rajada_alternada(8, lambda i: post(i, '/api/alugueis', _corpo_aluguel(d, automovel=d['automoveis'][1 + i % 8], dias_a_partir=100 + i * 5,
                                                                                 cupomCodigo=codigo)))
    usos = sum(1 for x in resp if x.status in (200, 201))
    r.checar('N02', CAT, 'Cupom de 1 uso, pedido 8 vezes (metade em cada instância), vale para no máximo um aluguel', '<= 1 aluguel com o cupom',
             f'{usos} aceitos; {sorted(x.status for x in resp)}', usos <= 1 and max(x.status for x in resp) < 500, 'media')

    # --- N03: devolução repetida ------------------------------------------------------------------------------------------------------------
    saida = _agora() + datetime.timedelta(seconds=5)
    corpo_al = _corpo_aluguel(d, automovel=d['automoveis'][9], dias_a_partir=0, duracao=2)
    corpo_al['dataSaida'], corpo_al['dataRetornoPrevisto'] = _iso(saida), _iso(saida + datetime.timedelta(days=2))
    aluguel = _id(post(0, '/api/alugueis', corpo_al))
    if aluguel:
        requisitar('POST', f'{base_a}/api/alugueis/{aluguel}/iniciar', token=t, ip=ip_unico(), json_corpo={'id': aluguel})
        dev = {'aluguelId': aluguel, 'dataDevolucao': _iso(_agora() + datetime.timedelta(days=2)), 'quilometragemFinal': 15000,
               'combustivelNoTanque': 50, 'nivelCombustivel': 'Cheio'}
        resp = _rajada_alternada(6, lambda i: post(i, '/api/devolucoes', dev))
        aceitas = sum(1 for x in resp if x.status in (200, 201))
        r.checar('N03', CAT, '6 devoluções do mesmo aluguel, metade em cada instância, registram uma só', '1 aceita',
                 f'{aceitas} aceitas; {sorted(x.status for x in resp)}', aceitas == 1 and max(x.status for x in resp) < 500, 'alta')

    # --- N04: funcionário com o mesmo CPF nas duas instâncias ---------------------------------------------------------------------------
    cpf = ambiente.cpf_valido()

    def funcionario(i):
        senha = ambiente.senha_forte()
        ctx.lembrar(senha)
        return post(i, '/api/funcionarios', {'nomeCompleto': f'Dupla{i}', 'cpf': cpf, 'email': f'dupla{i}-{secrets.token_hex(3)}@teste.local', 'senha': senha,
                                            'confirmarSenha': senha, 'salario': 3000,
                                            'admissaoEmUtc': _iso(_agora() - datetime.timedelta(days=30))})
    resp = _rajada_alternada(10, funcionario)
    aceitos = sum(1 for x in resp if x.status in (200, 201))
    r.checar('N04', CAT, '10 funcionários com o MESMO CPF, metade em cada instância, criam um só', '1 aceito',
             f'{aceitos} aceitos; {sorted(x.status for x in resp)}', aceitos == 1 and max(x.status for x in resp) < 500, 'media')

    # --- N05: iniciar e cancelar o mesmo aluguel nas duas instâncias ------------------------------------------------------------------------------
    al2 = _id(post(0, '/api/alugueis', _corpo_aluguel(d, automovel=d['automoveis'][10], dias_a_partir=200)))
    if al2:
        def acao(i):
            return requisitar('POST', f'{onde(i)}/api/alugueis/{al2}/{"iniciar" if i % 2 == 0 else "cancelar"}', token=t, ip=ip_unico(), json_corpo={'id': al2})
        res = _rajada_alternada(8, acao)
        estado = (requisitar('GET', f'{base_a}/api/alugueis/{al2}', token=t, ip=ip_unico()).json() or {}).get('status')
        r.checar('N05', CAT, 'Iniciar e cancelar o mesmo aluguel nas duas instâncias termina num estado final válido, sem 5xx',
                 'sem 5xx; Cancelado ou EmAndamento', f'{estado}; {sorted(x.status for x in res)}',
                 max(x.status for x in res) < 500 and estado in ('Cancelado', 'EmAndamento'), 'media')
