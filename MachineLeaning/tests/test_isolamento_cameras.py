"""S24 / S10 / S25: as câmeras pertencem a uma empresa; chat e módulo facial têm limite; o padrão do serviço falha fechado."""
import json
import os
import sys

import httpx
import pytest
from fastapi import FastAPI
from fastapi.testclient import TestClient

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from shared import auth, limites, settings

DONA = '11111111-1111-1111-1111-111111111111'
OUTRA = '22222222-2222-2222-2222-222222222222'


class _SessaoFalsa:
    """Web API falsa: devolve a identidade que o teste configurar para o token."""
    identidades: dict = {}
    chamadas = 0

    def __init__(self, *a, **k):
        pass

    async def __aenter__(self):
        return self

    async def __aexit__(self, *a):
        return False

    async def get(self, url, headers=None):
        type(self).chamadas += 1
        token = (headers or {}).get('Authorization', '').replace('Bearer ', '')
        dados = type(self).identidades.get(token)
        if dados is None:
            return httpx.Response(401, request=httpx.Request('GET', url))
        return httpx.Response(200, content=json.dumps(dados).encode(), request=httpx.Request('GET', url))


@pytest.fixture(autouse=True)
def _estado(monkeypatch):
    auth._sessoes_validas.clear()
    auth._tickets_stream.clear()
    limites.zerar()
    _SessaoFalsa.chamadas = 0
    _SessaoFalsa.identidades = {
        'tok-dona': {'usuarioId': 'A1', 'empresaId': DONA.upper(), 'cargo': 'Empresa'},
        'tok-func-dona': {'usuarioId': 'A2', 'empresaId': DONA, 'cargo': 'Funcionario'},
        'tok-outra': {'usuarioId': 'B1', 'empresaId': OUTRA, 'cargo': 'Empresa'},
        'tok-sem-empresa': {'usuarioId': 'C1', 'cargo': 'Empresa'},
    }
    monkeypatch.setattr(auth.httpx, 'AsyncClient', _SessaoFalsa)
    yield


@pytest.fixture
def cameras(monkeypatch):
    from yolo_tesseract import router as cam
    cam._detections.clear()
    cam.registrar_deteccao({'camera': 0, 'plate': 'AAA1B23', 'bbox': [0, 0, 1, 1]})
    app = FastAPI()
    app.include_router(cam.router)
    app.include_router(cam.stream_router)
    return TestClient(app)


def _h(token):
    return {'Authorization': f'Bearer {token}'}


# ---------- S24: quem pode ver câmeras, vídeo e placas ----------

def test_so_a_empresa_dona_das_cameras_ve_as_placas(cameras, monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)

    dona = cameras.get('/last', headers=_h('tok-dona'))
    assert dona.status_code == 200 and dona.json()['detections'][0]['plate'] == 'AAA1B23'
    assert cameras.get('/last', headers=_h('tok-func-dona')).status_code == 200   # funcionário da mesma empresa
    assert cameras.get('/last', headers=_h('tok-outra')).status_code == 403
    assert cameras.get('/last', headers=_h('tok-sem-empresa')).status_code == 403
    assert cameras.get('/last').status_code == 401


@pytest.mark.parametrize('metodo,url,corpo', [
    ('get', '/cameras', None), ('get', '/last', None), ('get', '/test/99', None),
    ('post', '/start', {'camera': 99}), ('post', '/stop/99', None), ('post', '/stream-ticket', None),
])
def test_outra_empresa_nao_acessa_nenhuma_rota_de_camera(cameras, monkeypatch, metodo, url, corpo):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)

    resposta = getattr(cameras, metodo)(url, headers=_h('tok-outra'), **({'json': corpo} if corpo else {}))

    assert resposta.status_code == 403


def test_ticket_de_stream_so_e_emitido_para_a_dona_e_carrega_a_identidade(cameras, monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)

    assert cameras.post('/stream-ticket', headers=_h('tok-outra')).status_code == 403
    ticket = cameras.post('/stream-ticket', headers=_h('tok-dona')).json()['ticket']
    assert auth._tickets_stream[ticket][1].empresa_id == DONA


def test_ticket_nao_vale_se_a_empresa_dona_mudar(monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)
    ticket = auth.emitir_ticket_stream(auth.Usuario('B1', OUTRA, 'Empresa'))['ticket']

    with pytest.raises(Exception) as e:
        auth.consumir_ticket_stream(ticket)

    assert getattr(e.value, 'status_code', None) == 403


def test_sem_empresa_configurada_producao_nega_tudo_e_desenvolvimento_libera(cameras, monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', None)

    monkeypatch.setattr(settings, 'em_desenvolvimento', lambda env=os.environ: False)
    assert cameras.get('/last', headers=_h('tok-dona')).status_code == 403

    monkeypatch.setattr(settings, 'em_desenvolvimento', lambda env=os.environ: True)
    assert cameras.get('/last', headers=_h('tok-outra')).status_code == 200


def test_identidade_vem_da_web_api_e_fica_em_cache(cameras, monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)

    for _ in range(3):
        assert cameras.get('/last', headers=_h('tok-dona')).status_code == 200

    assert _SessaoFalsa.chamadas == 1


# ---------- S10: chat exige login e tem limite; /face tem limite ----------

def test_chat_exige_login_e_limita_rajadas(monkeypatch):
    from ChatBot import router as chat

    async def resposta_fixa(mensagem, token):
        return 'ok'

    monkeypatch.setattr(chat, 'responder', resposta_fixa)
    app = FastAPI()
    app.include_router(chat.router)
    cliente = TestClient(app)

    assert cliente.post('/chat', json={'message': 'oi'}).status_code == 401

    codigos = [cliente.post('/chat', json={'message': 'oi'}, headers=_h('tok-dona')).status_code for _ in range(35)]
    assert codigos[:30] == [200] * 30
    assert set(codigos[30:]) == {429}
    # o limite é por usuário: outra pessoa não é afetada
    assert cliente.post('/chat', json={'message': 'oi'}, headers=_h('tok-outra')).status_code == 200


def test_limite_de_requisicoes_zera_depois_da_janela(monkeypatch):
    relogio = [1000.0]
    monkeypatch.setattr(limites.time, 'monotonic', lambda: relogio[0])

    assert all(limites._consumir('t', 'k', 3, 60) for _ in range(3))
    assert limites._consumir('t', 'k', 3, 60) is False
    relogio[0] += 61
    assert limites._consumir('t', 'k', 3, 60) is True


def test_modulo_facial_tem_limite_proprio(monkeypatch):
    from FaceAuth import router as face
    monkeypatch.setattr(face, 'extrair_landmarks_de_imagem', lambda imagem: [{'x': 0.1, 'y': 0.2, 'z': 0.3}])
    monkeypatch.setattr(face, '_decodificar_imagem', lambda b64: object())
    monkeypatch.setattr(face, '_vetor_do_rosto', lambda imagem, landmarks: [0.0] * 128)
    app = FastAPI()
    app.include_router(face.router)
    cliente = TestClient(app)
    h = {'X-Internal-Token': settings.INTERNAL_TOKEN}

    codigos = [cliente.post('/face/landmarks', json={'imageBase64': 'x'}, headers=h).status_code for _ in range(125)]

    assert codigos[:120] == [200] * 120
    assert 429 in codigos[120:]


def test_cameras_tem_limite_por_usuario_e_acoes_tem_limite_mais_estrito(cameras, monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)

    # ações (liga/desliga/ticket): 30 por minuto por usuário
    acoes = [cameras.post('/stream-ticket', headers=_h('tok-dona')).status_code for _ in range(33)]
    assert acoes[:30] == [200] * 30 and set(acoes[30:]) == {429}
    # a leitura continua liberada para o mesmo usuário (contador à parte) e outro usuário não é afetado
    assert cameras.get('/last', headers=_h('tok-dona')).status_code == 200
    assert cameras.post('/stream-ticket', headers=_h('tok-func-dona')).status_code == 200

    limites.zerar()
    leituras = [cameras.get('/last', headers=_h('tok-dona')).status_code for _ in range(305)]
    assert leituras[:300] == [200] * 300 and set(leituras[300:]) == {429}


def test_quem_nao_e_da_empresa_leva_403_antes_de_gastar_o_limite(cameras, monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)

    codigos = {cameras.post('/stream-ticket', headers=_h('tok-outra')).status_code for _ in range(40)}

    assert codigos == {403}


def test_stream_tem_limite_por_ip_mesmo_com_ticket_invalido(cameras, monkeypatch):
    monkeypatch.setattr(settings, 'CAMERA_EMPRESA_ID', DONA)

    codigos = [cameras.get('/stream/0?ticket=falso').status_code for _ in range(24)]

    assert set(codigos[:20]) == {401}
    assert set(codigos[20:]) == {429}


# ---------- S25: o padrão do serviço falha fechado ----------

def test_escuta_so_na_propria_maquina_por_padrao():
    assert settings.HOST == '127.0.0.1'


def test_main_nao_escuta_em_todas_as_interfaces_nem_recarrega_em_producao():
    fonte = open(os.path.join(os.path.dirname(__file__), '..', 'main.py'), encoding='utf-8').read()
    assert 'host="0.0.0.0"' not in fonte
    assert 'reload=True' not in fonte
    assert 'reload=settings.em_desenvolvimento()' in fonte
