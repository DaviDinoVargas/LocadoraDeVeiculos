"""Cenários de carga: vários usuários simultâneos contra leitura, escrita e login. Só mede e registra; os limites
de aceitação ficam em LIMITES (ajuste com consciência: são o critério de "passou")."""
import statistics
import threading
import time
from concurrent.futures import ThreadPoolExecutor

from lib import ambiente
from lib.http import ip_unico, requisitar

CAT = 'Carga'
DURACAO_S = 10

# Critérios de aceitação por cenário (ambiente local, banco no SQL Server em container).
LIMITES = {'erros_5xx': 0, 'p95_leitura_ms': 1000, 'p95_login_ms': 3000}


def _percentil(valores, p):
    if not valores:
        return 0.0
    ordenados = sorted(valores)
    return ordenados[min(len(ordenados) - 1, int(round(p / 100 * (len(ordenados) - 1))))]


def _rodar(nome, fabrica_requisicao, concorrencia, duracao=DURACAO_S):
    tempos, status = [], []
    trava = threading.Lock()
    fim = time.perf_counter() + duracao

    def trabalhador():
        while time.perf_counter() < fim:
            resp = fabrica_requisicao()
            with trava:
                tempos.append(resp.ms)
                status.append(resp.status if not resp.erro else 0)

    inicio = time.perf_counter()
    with ThreadPoolExecutor(max_workers=concorrencia) as pool:
        for _ in range(concorrencia):
            pool.submit(trabalhador)
    total = time.perf_counter() - inicio
    ok = [s for s in status if 200 <= s < 300]
    return {
        'cenario': nome, 'concorrencia': concorrencia, 'duracao_s': round(total, 1), 'requisicoes': len(status),
        'rps': round(len(status) / total, 1), 'ok': len(ok),
        'erros_4xx': len([s for s in status if 400 <= s < 500]), 'erros_429': status.count(429),
        'erros_5xx': len([s for s in status if s >= 500]), 'falhas_conexao': status.count(0),
        'p50_ms': round(_percentil(tempos, 50), 1), 'p95_ms': round(_percentil(tempos, 95), 1),
        'p99_ms': round(_percentil(tempos, 99), 1), 'max_ms': round(max(tempos or [0]), 1),
        'media_ms': round(statistics.mean(tempos), 1) if tempos else 0,
    }


def executar(ctx, servico_api=None):
    """Retorna a lista de medições e registra os critérios de aceitação em ctx.reg."""
    r, base = ctx.reg, ctx.base
    emp = ambiente.nova_empresa(base, 'carga')
    ctx.lembrar(emp.senha, emp.token)
    ip = ip_unico()

    # massa de dados realista: 30 clientes
    import datetime
    for i in range(30):
        requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp.token, ip=ip, json_corpo={
            'nome': f'Cliente de carga {i}', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': '12345678901',
            'validadeCnh': (datetime.datetime.now() + datetime.timedelta(days=900)).isoformat(),
            'telefone': '(11) 99999-0000', 'email': f'c{i}@teste.local', 'endereco': 'Rua Exemplo X, 100, Centro'})

    medicoes = []
    leituras = [('GET /api/clientes', lambda: requisitar('GET', f'{base}/api/clientes', token=emp.token, ip=ip, reutilizar=True)),
                ('GET /api/dashboard/resumo', lambda: requisitar('GET', f'{base}/api/dashboard/resumo', token=emp.token, ip=ip, reutilizar=True)),
                ('GET /api/automoveis', lambda: requisitar('GET', f'{base}/api/automoveis', token=emp.token, ip=ip, reutilizar=True))]
    for nome, fn in leituras:
        for conc in (10, 50, 100):
            m = _rodar(nome, fn, conc)
            medicoes.append(m)
            print(f"  {nome:28} conc={conc:<3} {m['rps']:>7} req/s  p50={m['p50_ms']:>7} p95={m['p95_ms']:>7} p99={m['p99_ms']:>7} ms  ok={m['ok']}/{m['requisicoes']} 5xx={m['erros_5xx']} 429={m['erros_429']}", flush=True)

    # login: PBKDF2 com 210.000 iterações é caro de propósito; conta separada para não depender do lockout
    login_conta = ambiente.nova_empresa(base, 'carga-login')
    ctx.lembrar(login_conta.senha)
    for conc in (5, 20):
        m = _rodar('POST /api/auth/autenticar', lambda: ambiente.login(base, login_conta.email, login_conta.senha, ip=ip_unico(), reutilizar=True), conc)
        medicoes.append(m)
        print(f"  {'POST /api/auth/autenticar':28} conc={conc:<3} {m['rps']:>7} req/s  p50={m['p50_ms']:>7} p95={m['p95_ms']:>7} p99={m['p99_ms']:>7} ms  5xx={m['erros_5xx']} 429={m['erros_429']}", flush=True)

    # carga com credencial errada: não pode haver 5xx nem travar o servidor
    m = _rodar('POST /api/auth/autenticar (senha errada)', lambda: ambiente.login(base, f'x{ip_unico()}@teste.local', 'Errada@123456', ip=ip_unico(), reutilizar=True), 20)
    medicoes.append(m)

    # --- carga MISTA: leituras e escritas ao mesmo tempo, com os invariantes conferidos no banco depois -------------------------------------
    medicoes.append(_carga_mista(ctx, base))

    memoria = servico_api.memoria_mb() if servico_api else None

    sem_5xx = [m for m in medicoes if m['erros_5xx'] > LIMITES['erros_5xx']]
    r.checar('L01', CAT, 'Nenhum erro 5xx sob carga em nenhum cenário', '0', [(m['cenario'], m['concorrencia'], m['erros_5xx']) for m in sem_5xx] or 0,
             not sem_5xx, 'alta')
    sem_conexao = [m for m in medicoes if m['falhas_conexao'] > 0]
    r.checar('L02', CAT, 'Nenhuma conexão derrubada sob carga', '0', [(m['cenario'], m['concorrencia'], m['falhas_conexao']) for m in sem_conexao] or 0,
             not sem_conexao, 'media')
    lentas = [m for m in medicoes if m['cenario'].startswith('GET') and m['concorrencia'] <= 50 and m['p95_ms'] > LIMITES['p95_leitura_ms']]
    r.checar('L03', CAT, f"p95 das leituras até 50 usuários simultâneos abaixo de {LIMITES['p95_leitura_ms']} ms", 'dentro do limite',
             [(m['cenario'], m['concorrencia'], m['p95_ms']) for m in lentas] or 'ok', not lentas, 'media')
    lentos_login = [m for m in medicoes if m['cenario'] == 'POST /api/auth/autenticar' and m['p95_ms'] > LIMITES['p95_login_ms']]
    r.checar('L04', CAT, f"p95 do login abaixo de {LIMITES['p95_login_ms']} ms (hash caro de propósito)", 'dentro do limite',
             [(m['concorrencia'], m['p95_ms']) for m in lentos_login] or 'ok', not lentos_login, 'baixa')
    if memoria is not None:
        r.info('L05', CAT, 'Memória do processo da API ao fim da carga (MB)', memoria)

    return {'medicoes': medicoes, 'memoria_api_mb': memoria}


def _carga_mista(ctx, base):
    """50% leituras de listas, 10% painel, 10% cadastro de cliente, 20% reserva de aluguel (disputando 4 carros), 10% cancelamento, por 20
    usuários simultâneos. Depois confere o que importa de verdade: nenhum 5xx, nada que o servidor confirmou se perdeu, nada duplicou e
    nenhum carro ficou com dois aluguéis ativos no mesmo período."""
    import datetime
    import random

    from ataques.h_negocio import _corpo_aluguel, _montar
    from lib.http import id_de

    r = ctx.reg
    emp = ambiente.nova_empresa(base, 'carga-mista')
    ctx.lembrar(emp.senha, emp.token)
    d = _montar(base, emp, carros=4)
    if not d:
        r.info('L06', CAT, 'Cenário da carga mista não pôde ser montado; ignorada', '', '')
        return {'cenario': 'misto (não executado)', 'concorrencia': 0, 'rps': 0, 'p50_ms': 0, 'p95_ms': 0, 'p99_ms': 0,
                'erros_4xx': 0, 'erros_429': 0, 'erros_5xx': 0, 'falhas_conexao': 0}

    ip = ip_unico()
    criados_clientes, criados_alugueis, tempos_escrita = [], [], []
    trava = threading.Lock()

    def get(caminho):
        return requisitar('GET', f'{base}{caminho}', token=emp.token, ip=ip, reutilizar=True)

    def uma_operacao():
        x = random.random()
        if x < 0.30:
            return get('/api/clientes')
        if x < 0.40:
            return get('/api/automoveis')
        if x < 0.50:
            return get('/api/alugueis')
        if x < 0.60:
            return get('/api/dashboard/resumo')
        if x < 0.70:
            resp = requisitar('POST', f'{base}/api/clientes/pessoa-fisica', token=emp.token, ip=ip, reutilizar=True, json_corpo={
                'nome': 'Cliente misto', 'cpf': ambiente.cpf_valido(), 'rg': '123456789', 'cnh': '12345678901',
                'validadeCnh': (datetime.datetime.now() + datetime.timedelta(days=900)).isoformat(),
                'telefone': '(11) 99999-0000', 'email': 'misto@teste.local', 'endereco': 'Rua Exemplo X, 100, Centro'})
            with trava:
                tempos_escrita.append(resp.ms)
                if resp.status == 201:
                    criados_clientes.append(id_de(resp))
            return resp
        if x < 0.90:
            corpo = _corpo_aluguel(d, automovel=random.choice(d['automoveis']), dias_a_partir=random.randint(1, 60), duracao=random.randint(1, 3))
            resp = requisitar('POST', f'{base}/api/alugueis', token=emp.token, ip=ip, reutilizar=True, json_corpo=corpo)
            with trava:
                tempos_escrita.append(resp.ms)
                if resp.status == 201:
                    criados_alugueis.append(id_de(resp))
            return resp
        with trava:
            alvo = random.choice(criados_alugueis) if criados_alugueis else None
        if not alvo:
            return get('/api/alugueis/em-aberto')
        resp = requisitar('POST', f'{base}/api/alugueis/{alvo}/cancelar', token=emp.token, ip=ip, reutilizar=True, json_corpo={'id': alvo})
        with trava:
            tempos_escrita.append(resp.ms)
        return resp

    m = _rodar('misto leitura+escrita', uma_operacao, 20, duracao=15)
    print(f"  {'misto leitura+escrita':28} conc=20  {m['rps']:>7} req/s  p50={m['p50_ms']:>7} p95={m['p95_ms']:>7} p99={m['p99_ms']:>7} ms  5xx={m['erros_5xx']} 429={m['erros_429']}", flush=True)

    clientes = get('/api/clientes').json() or {}
    clientes = clientes.get('registros', []) if isinstance(clientes, dict) else []
    ids_lidos = [str(c.get('id')).lower() for c in clientes if isinstance(c, dict)]
    perdidos = [c for c in criados_clientes if str(c).lower() not in ids_lidos]
    r.checar('L06', CAT, 'Carga mista: nenhum 5xx e nenhuma conexão derrubada com leitura e escrita ao mesmo tempo', '0 erros',
             f"5xx={m['erros_5xx']} conexões={m['falhas_conexao']}", m['erros_5xx'] == 0 and m['falhas_conexao'] == 0, 'alta')
    r.checar('L07', CAT, 'Carga mista: todo cliente que o servidor confirmou (201) existe no banco, sem duplicar nem perder', 'todos presentes, lista sem repetição',
             f'{len(criados_clientes)} confirmados; {len(perdidos)} perdidos; {len(ids_lidos) - len(set(ids_lidos))} repetidos',
             not perdidos and len(ids_lidos) == len(set(ids_lidos)), 'alta')

    lista = get('/api/alugueis').json() or {}
    itens = lista.get('registros', []) if isinstance(lista, dict) else []
    por_carro = {}
    for a in itens:
        if isinstance(a, dict) and a.get('status') != 'Cancelado':
            por_carro.setdefault(a.get('automovelPlaca'), []).append((a.get('dataSaida'), a.get('dataRetornoPrevisto')))
    sobrepostos = []
    for placa, periodos in por_carro.items():
        periodos.sort()
        for (ini1, fim1), (ini2, fim2) in zip(periodos, periodos[1:]):
            if ini2 < fim1:
                sobrepostos.append((placa, ini2))
    r.checar('L08', CAT, 'Carga mista: nenhum carro termina com dois aluguéis ativos sobrepostos', '0 sobreposições',
             f'{len(sobrepostos)} em {len(itens)} aluguéis lidos ({len(criados_alugueis)} criados)', not sobrepostos, 'alta')
    r.checar('L09', CAT, 'Carga mista: p95 das ESCRITAS abaixo de 3 s mesmo com a fila por empresa', 'p95 < 3000 ms',
             f'{round(_percentil(tempos_escrita, 95), 1)} ms em {len(tempos_escrita)} escritas', _percentil(tempos_escrita, 95) < 3000, 'media')
    return m
