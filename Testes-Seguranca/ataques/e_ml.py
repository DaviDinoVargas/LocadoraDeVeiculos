"""Ataques ao serviço de ML (rodando com ML_ENV=production, token interno forte e hash do peso)."""
from lib import ambiente
from lib.http import ip_unico, requisitar

CAT = 'ML'


def executar(ctx):
    r = ctx.reg
    if not ctx.ml:
        r.info('E00', CAT, 'ML não foi iniciado; ataques ao ML ignorados', 'use --com-ml', '')
        return
    ml, base = ctx.ml, ctx.base

    # --- E01: módulo facial só com o token interno ---------------------------------------------------------
    rotas = [('POST', '/face/landmarks', {'imageBase64': 'x'}), ('POST', '/face/compare', {'imageBase64': 'x', 'samples': [[0.1, 0.2, 0.3]]})]
    sem_token = [(m, p, requisitar(m, f'{ml}{p}', json_corpo=c).status) for m, p, c in rotas]
    r.checar('E01a', CAT, '/face/* sem token interno recebe 401', '401', [x for x in sem_token if x[2] != 401] or '401', all(x[2] == 401 for x in sem_token), 'alta')
    errado = [requisitar(m, f'{ml}{p}', json_corpo=c, cabecalhos={'X-Internal-Token': 'dev-only-shared-secret-troque-em-producao'}).status for m, p, c in rotas]
    r.checar('E01b', CAT, '/face/* com o token PADRÃO PÚBLICO do repositório recebe 401', '401', errado, all(s == 401 for s in errado), 'alta', 'S2')
    certo = requisitar('POST', f'{ml}/face/landmarks', json_corpo={'imageBase64': 'x'}, cabecalhos={'X-Internal-Token': ctx.token_ml})
    r.checar('E01c', CAT, 'Com o token certo o endpoint responde (400 por imagem inválida, não 401/500)', '400', certo.status, certo.status == 400)
    grande = requisitar('POST', f'{ml}/face/landmarks', json_corpo={'imageBase64': 'A' * 4_100_000}, cabecalhos={'X-Internal-Token': ctx.token_ml})
    r.checar('E01d', CAT, 'Imagem acima de 4 milhões de caracteres é recusada (422)', '422', grande.status, grande.status == 422, 'media', 'S9')
    antigas = [requisitar('POST', f'{ml}{p}', json_corpo={'personId': 'x', 'imageBase64': 'x'}, cabecalhos={'X-Internal-Token': ctx.token_ml}).status
               for p in ('/face/enroll', '/face/verify')]
    r.checar('E01e', CAT, 'Rotas antigas que guardavam dados (enroll/verify) não existem mais', '404/405', antigas, all(s in (404, 405) for s in antigas), 'media', 'S1')

    # --- E02: câmeras exigem login -----------------------------------------------------------------------------
    anon = [(p, requisitar('GET', f'{ml}{p}').status) for p in ('/cameras', '/last', '/test/0')] + \
           [('/start', requisitar('POST', f'{ml}/start', json_corpo={'camera': 0}).status)]
    r.checar('E02a', CAT, 'Câmeras sem JWT recebem 401', '401 em todas', [x for x in anon if x[1] != 401] or '401', all(x[1] == 401 for x in anon), 'alta')
    stream = requisitar('GET', f'{ml}/stream/0?ticket=inventado')
    r.checar('E02b', CAT, 'Stream com ticket inventado é recusado', '401', stream.status, stream.status == 401, 'alta')

    # --- E03: isolamento entre empresas no ML (S24) ---------------------------------------------------------------
    emp = ctx.dona_das_cameras  # a empresa configurada como dona das câmeras
    outra = ambiente.nova_empresa(base, 'ml2')
    ctx.lembrar(outra.senha, outra.token)
    # /last não toca em hardware; /cameras sondaria as webcams reais da máquina, então não é usado com credencial válida.
    cam_empresa = requisitar('GET', f'{ml}/last', token=emp.token)
    r.checar('E03a', CAT, 'ML aceita a empresa dona das câmeras (controle: o fluxo legítimo funciona)', '200', cam_empresa.status, cam_empresa.status == 200)
    # S24: nada liga a conta que consulta à empresa dona das câmeras
    estranha = requisitar('GET', f'{ml}/last', token=outra.token)
    r.checar('E03b', CAT, 'Uma empresa que NÃO é dona das câmeras não vê as placas detectadas', '403', estranha.status,
             estranha.status in (401, 403), 'alta', 'S24')
    liga = requisitar('POST', f'{ml}/start', token=outra.token, json_corpo={'camera': 99})  # índice inexistente: nunca liga a webcam real
    r.checar('E03c', CAT, 'Uma empresa que NÃO é dona das câmeras não liga o monitoramento', '403', liga.status,
             liga.status in (401, 403), 'alta', 'S24')
    ticket = requisitar('POST', f'{ml}/stream-ticket', token=outra.token)
    r.checar('E03d', CAT, 'Uma empresa que NÃO é dona das câmeras não obtém ticket de vídeo', '403', ticket.status,
             ticket.status in (401, 403), 'alta', 'S24')

    # --- E04: superfície pública e abuso ------------------------------------------------------------------------------
    docs = [requisitar('GET', f'{ml}{p}').status for p in ('/docs', '/openapi.json', '/redoc')]
    r.checar('E04a', CAT, 'Em produção /docs, /openapi.json e /redoc não existem', '404', docs, all(s == 404 for s in docs), 'media', 'S12')
    health = requisitar('GET', f'{ml}/health').json() or {}
    r.checar('E04b', CAT, 'Em produção /health anônimo não detalha versões nem câmeras', 'só status', sorted(health), set(health) <= {'status'}, 'baixa', 'S12')
    chat = requisitar('POST', f'{ml}/chat', json_corpo={'message': 'oi'})
    r.checar('E04c', CAT, '/chat exige login (não responde anônimo)', '401', chat.status, chat.status == 401, 'baixa', 'S10')
    longa = requisitar('POST', f'{ml}/chat', token=emp.token, json_corpo={'message': 'x' * 2001})
    r.checar('E04d', CAT, 'Mensagem de chat acima de 2000 caracteres é recusada', '422', longa.status, longa.status == 422, 'baixa', 'S9')
    codigos = [requisitar('POST', f'{ml}/chat', token=emp.token, ip=ip_unico(), json_corpo={'message': 'oi'}).status for _ in range(80)]
    r.checar('E04e', CAT, 'O ML limita rajadas de /chat (algum 429 em 80 pedidos seguidos)', 'algum 429', f'{codigos.count(429)} de 80',
             codigos.count(429) > 0, 'baixa', 'S10')
