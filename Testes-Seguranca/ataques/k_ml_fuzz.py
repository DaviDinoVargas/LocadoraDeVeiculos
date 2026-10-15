"""Fuzz do serviço de ML: imagens corrompidas e "bombas" de descompressão em /face, JSON hostil e texto adversário em /chat, corpos gigantes e
parâmetros de câmera fora do esperado. O critério é sempre o mesmo: o serviço responde com um erro CONTROLADO (4xx), nunca com 500, não trava e
não incha a memória; depois do ataque ele continua respondendo (/health) e o fluxo legítimo continua funcionando."""
import base64
import json
import struct
import threading
import time
import zlib

from lib import ambiente
from lib.http import ip_unico, requisitar

CAT = 'ML (fuzz)'


def _b64(dados: bytes) -> str:
    return base64.b64encode(dados).decode()


def _png(largura: int, altura: int, rgb=False) -> bytes:
    """PNG válido de uma cor só, com as dimensões pedidas. Comprime muito (zeros): o arquivo é pequeno e a imagem decodificada é enorme."""
    def bloco(tipo: bytes, dados: bytes) -> bytes:
        return struct.pack('>I', len(dados)) + tipo + dados + struct.pack('>I', zlib.crc32(tipo + dados) & 0xFFFFFFFF)

    canais = 3 if rgb else 1
    cab = struct.pack('>IIBBBBB', largura, altura, 8, 2 if rgb else 0, 0, 0, 0)
    comp = zlib.compressobj(9)
    linha = bytes(1 + largura * canais)  # filtro 0 + pixels zero
    partes = [comp.compress(linha) for _ in range(altura)]
    partes.append(comp.flush())
    return b'\x89PNG\r\n\x1a\n' + bloco(b'IHDR', cab) + bloco(b'IDAT', b''.join(partes)) + bloco(b'IEND', b'')


class _VigiaDeMemoria:
    """Anota o pico de memória do processo do ML enquanto o ataque roda."""

    def __init__(self, servico):
        self.servico, self.pico, self._parar = servico, 0.0, threading.Event()
        self._fio = threading.Thread(target=self._rodar, daemon=True)

    def _rodar(self):
        while not self._parar.is_set():
            m = self.servico.memoria_mb() if self.servico else None
            if m:
                self.pico = max(self.pico, m)
            time.sleep(0.05)

    def __enter__(self):
        self._fio.start()
        return self

    def __exit__(self, *_):
        self._parar.set()
        self._fio.join(timeout=2)


def executar(ctx):
    r = ctx.reg
    if not ctx.ml:
        r.info('K00', CAT, 'ML não foi iniciado; fuzz do ML ignorado', '', '')
        return
    ml = ctx.ml
    interno = {'X-Internal-Token': ctx.token_ml}
    dona = ctx.dona_das_cameras

    def face(corpo=None, bruto=None, rota='/face/landmarks', timeout=60):
        return requisitar('POST', f'{ml}{rota}', json_corpo=corpo, bruto=bruto, cabecalhos=interno, timeout=timeout)

    def vivo():
        return requisitar('GET', f'{ml}/health', timeout=10).status == 200

    # --- K01: imagens corrompidas ou que não são imagem ---------------------------------------------------------------------------
    jpeg_curto = b'\xff\xd8\xff\xe0\x00\x10JFIF\x00\x01\x01\x00\x00\x01\x00\x01\x00\x00' + bytes(40)   # cabeçalho JPEG e depois nada
    casos = {
        'string vazia': '',
        'base64 inválido': '@@@@!!!!####',
        'base64 válido de texto': _b64(b'isto nao e uma imagem'),
        'JPEG truncado': _b64(jpeg_curto),
        'PNG só com a assinatura': _b64(b'\x89PNG\r\n\x1a\n'),
        'bytes aleatórios': _b64(bytes(range(256)) * 40),
        'zip no lugar da imagem': _b64(b'PK\x03\x04' + bytes(200)),
        'data URL com tipo errado': 'data:text/html;base64,' + _b64(b'<script>alert(1)</script>'),
        'base64 com byte nulo': _b64(b'\x00' * 500),
        'só a vírgula': ',',
    }
    ruins = {}
    for nome, imagem in casos.items():
        x = face({'imageBase64': imagem})
        if x.status not in (400, 422):
            ruins[nome] = x.status
    r.checar('K01', CAT, 'Imagens corrompidas ou que não são imagem em /face/landmarks recebem 400/422 (nunca 500 nem 200)', '400/422 em todos',
             ruins or '400/422', not ruins, 'alta', 'S9')
    r.checar('K01b', CAT, 'Depois dos arquivos corrompidos o ML continua de pé', '/health 200', 'vivo' if vivo() else 'caiu', vivo(), 'alta')

    # --- K02: bomba de descompressão (PNG pequeno que vira imagem enorme na memória) -----------------------------------------------
    bomba = _png(12000, 12000)           # ~144 milhões de pixels: centenas de MB na memória se for decodificada
    corpo_bomba = {'imageBase64': _b64(bomba)}
    antes = ctx.ml_servico.memoria_mb() if ctx.ml_servico else None
    with _VigiaDeMemoria(ctx.ml_servico) as vigia:
        inicio = time.perf_counter()
        resp = face(corpo_bomba)
        gasto = time.perf_counter() - inicio
    crescimento = round(vigia.pico - antes, 1) if antes else None
    r.checar('K02', CAT, f'PNG de {len(bomba) // 1024} KB que viraria 144 milhões de pixels é recusado antes de decodificar',
             '400/413/422 e crescimento de memória < 300 MB',
             f'status {resp.status} em {gasto:.1f}s; memória {antes} -> pico {vigia.pico} MB (+{crescimento})',
             resp.status in (400, 413, 422) and (crescimento is None or crescimento < 300), 'alta', 'S9')
    r.checar('K02b', CAT, 'Depois da bomba o ML continua de pé', '/health 200', 'vivo' if vivo() else 'caiu', vivo(), 'alta')

    # --- K03: corpos fora de padrão ou gigantes ------------------------------------------------------------------------------------
    deformados = {
        'JSON inválido': b'{"imageBase64": ',
        'lista no lugar do objeto': b'[1,2,3]',
        'campo com número': json.dumps({'imageBase64': 12345}).encode(),
        'campo nulo': json.dumps({'imageBase64': None}).encode(),
        'sem o campo': b'{}',
        'UTF-8 inválido': b'{"imageBase64": "\xff\xfe\xfd"}',
        'aninhamento de 5000 níveis': b'[' * 5000 + b']' * 5000,
        'chave duplicada': b'{"imageBase64":"x","imageBase64":"y"}',
    }
    ruins = {}
    for nome, bruto in deformados.items():
        x = face(bruto=bruto)
        if x.status not in (400, 422):
            ruins[nome] = x.status
    r.checar('K03', CAT, 'Corpos mal formados em /face recebem 400/422 (nunca 500)', '400/422 em todos', ruins or '400/422', not ruins, 'media', 'S9')

    with _VigiaDeMemoria(ctx.ml_servico) as vigia:
        antes = ctx.ml_servico.memoria_mb() if ctx.ml_servico else None
        enorme = face(bruto=b'{"imageBase64": "' + b'A' * (30 * 1024 * 1024) + b'"}', timeout=120)
    crescimento = round(vigia.pico - antes, 1) if antes else None
    r.checar('K04', CAT, 'Corpo de 30 MB em /face é recusado (4xx) sem inchar a memória do ML', '4xx e crescimento < 300 MB',
             f'status {enorme.status}; +{crescimento} MB (erro de rede: {enorme.erro})',
             (400 <= enorme.status < 500 or enorme.erro is not None) and (crescimento is None or crescimento < 300), 'media', 'S9')
    r.checar('K04b', CAT, 'Depois do corpo gigante o ML continua de pé', '/health 200', 'vivo' if vivo() else 'caiu', vivo(), 'alta')

    # --- K05: /chat com texto adversário ------------------------------------------------------------------------------------------------
    emp = dona
    ctx.lembrar(emp.senha, emp.token)

    def chat(mensagem=None, bruto=None, tipo='application/json'):
        cab = {'Content-Type': tipo}
        return requisitar('POST', f'{ml}/chat', token=emp.token, ip=ip_unico(), json_corpo=None if bruto else {'message': mensagem},
                          bruto=bruto, cabecalhos=cab if bruto else None, timeout=30)

    hostis = {
        'regex da placa em loop (2000 letras)': 'A' * 2000,
        'placas coladas': 'ABC1D23 ' * 250,
        'só espaços': ' ' * 2000,
        'RTL e zero-width': '‮​‍' * 600,
        'emoji': '😀' * 500,
        'byte nulo': 'qual a placa \x00 ABC1D23',
        'HTML': '<script>alert(1)</script><img src=x onerror=alert(1)>',
        'SQL': "'; DROP TABLE Alugueis; --",
        'template': '{{7*7}} ${7*7} #{7*7}',
        'path': '../../../../etc/passwd',
        'vazia': '',
    }
    ruins, lentos, ecos = {}, {}, {}
    for nome, texto in hostis.items():
        x = chat(texto)
        if x.status >= 500 or x.status == 0:
            ruins[nome] = x.status or x.erro
        if x.ms > 3000:
            lentos[nome] = round(x.ms)
        resposta = (x.json() or {}).get('reply', '') if x.ok else ''
        if '<script' in resposta.lower() or 'onerror' in resposta.lower():
            ecos[nome] = 'devolveu o HTML recebido'
    r.checar('K05', CAT, 'Textos adversários no /chat (regex em loop, unicode, HTML, SQL, nulo) não geram 500 nem demoram mais de 3 s', 'sem 5xx e rápido',
             {**ruins, **{k: f'{v} ms' for k, v in lentos.items()}} or 'ok', not ruins and not lentos, 'media', 'S10')
    r.checar('K05b', CAT, 'O /chat nunca devolve o HTML recebido (a resposta é texto fixo montado pelo servidor)', 'nenhum eco', ecos or 'nenhum', not ecos, 'media', 'S10')

    tipos_errados = {
        'mensagem numérica': json.dumps({'message': 123}).encode(),
        'mensagem lista': json.dumps({'message': ['a']}).encode(),
        'mensagem nula': json.dumps({'message': None}).encode(),
        'objeto vazio': b'{}',
        'JSON inválido': b'{"message": ',
        'aninhamento de 5000 níveis': b'[' * 5000 + b']' * 5000,
        'UTF-8 inválido': b'{"message": "\xff\xfe"}',
    }
    ruins = {}
    for nome, bruto in tipos_errados.items():
        x = chat(bruto=bruto)
        if x.status not in (400, 422):
            ruins[nome] = x.status
    texto_plano = chat(bruto=b'oi', tipo='text/plain')
    form = chat(bruto=b'message=oi', tipo='application/x-www-form-urlencoded')
    r.checar('K06', CAT, '/chat com JSON de tipo errado ou mal formado recebe 400/422, e com Content-Type que não é JSON também (nunca 500)',
             '400/422', {**ruins, **({'text/plain': texto_plano.status} if texto_plano.status not in (400, 415, 422) else {}),
                         **({'form': form.status} if form.status not in (400, 415, 422) else {})} or '400/422',
             not ruins and texto_plano.status in (400, 415, 422) and form.status in (400, 415, 422), 'media', 'S9')

    # --- K07: parâmetros de câmera fora do esperado (índices que NÃO existem: a webcam real nunca é aberta) --------------------------------
    def acao(metodo, rota, corpo=None):
        x = requisitar(metodo, f'{ml}{rota}', token=dona.token, json_corpo=corpo, ip=ip_unico())
        if x.status == 429:   # o limite de ações (30/min) pode ter sido gasto por E05: espera a janela e repete uma vez
            time.sleep(61)
            x = requisitar(metodo, f'{ml}{rota}', token=dona.token, json_corpo=corpo, ip=ip_unico())
        return x

    estranhos = {
        'camera negativa': {'camera': -1},
        'camera enorme': {'camera': 99999999999999999999},
        'camera texto': {'camera': 'abc'},
        'camera lista': {'camera': [0]},
        'camera nula': {'camera': None},
        'fps NaN': {'camera': 99, 'fps': 'NaN'},
        'fps infinito': {'camera': 99, 'fps': 1e999},
        'fps zero': {'camera': 99, 'fps': 0},
        'fps negativo': {'camera': 99, 'fps': -5},
        'fps gigante': {'camera': 99, 'fps': 100000},
        'campo extra malicioso': {'camera': 99, 'rtsp': 'rtsp://127.0.0.1:9/x', 'url': 'http://127.0.0.1:1/'},
    }
    ruins = {}
    for nome, corpo in estranhos.items():
        x = requisitar('POST', f'{ml}/start', token=dona.token, ip=ip_unico(), bruto=json.dumps(corpo).encode(), timeout=30)
        if x.status == 429:
            time.sleep(61)
            x = requisitar('POST', f'{ml}/start', token=dona.token, ip=ip_unico(), bruto=json.dumps(corpo).encode(), timeout=30)
        if x.status >= 500 or x.status == 0 or x.ok:
            ruins[nome] = x.status
    r.checar('K07', CAT, '/start com índice de câmera ou fps fora do esperado é recusado (4xx), nunca 500 e nunca "ok"', '4xx em todos',
             ruins or '4xx', not ruins, 'alta', 'S10')

    ruins = {}
    for rota in ('/stop/abc', '/stop/-1', '/stop/99999999999999999999', '/test/abc', '/test/99999999999999999999', '/stream/abc?ticket=x'):
        metodo = 'POST' if rota.startswith('/stop') else 'GET'
        x = acao(metodo, rota)
        if x.status >= 500 or x.status == 0:
            ruins[rota] = x.status
    r.checar('K08', CAT, 'Ids de câmera inválidos nas rotas /stop, /test e /stream dão 4xx (nunca 500)', 'sem 5xx', ruins or 'ok', not ruins, 'media', 'S10')

    r.checar('K09', CAT, 'Ao fim de todo o fuzz o ML continua de pé e o /chat legítimo continua respondendo', '/health 200 e /chat 200',
             f'health {"ok" if vivo() else "caiu"}; chat {chat("quantos clientes temos?").status}', vivo() and chat('oi').status == 200, 'alta')
