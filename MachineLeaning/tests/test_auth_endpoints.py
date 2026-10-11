"""Autenticação dos endpoints do ML: token interno (módulo facial), JWT validado
na Web API (câmeras) e ticket de uso único (stream)."""
import os
import sys
import time

import pytest
from fastapi import Depends, FastAPI, HTTPException
from fastapi.testclient import TestClient

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from shared import auth, settings


@pytest.fixture(autouse=True)
def _limpar_estado():
    auth._sessoes_validas.clear()
    auth._tickets_stream.clear()
    yield


# ---------- módulo facial: token interno em TODOS os endpoints ----------

@pytest.fixture
def face_client(tmp_path, monkeypatch):
    from FaceAuth import data_manager
    from FaceAuth.router import router
    monkeypatch.setattr(data_manager, '_DATA_DIR', str(tmp_path))
    monkeypatch.setattr(data_manager, '_DATA_FILE', str(tmp_path / 'faces.json'))
    data_manager.salvar_amostra('usuario:alvo@x.com', [0.1, 0.2])
    app = FastAPI()
    app.include_router(router)
    return TestClient(app)


@pytest.mark.parametrize('metodo,url', [
    ('post', '/face/enroll'),
    ('post', '/face/verify'),
    ('get', '/face/status/usuario:alvo@x.com'),
    ('delete', '/face/usuario:alvo@x.com'),
])
def test_face_sem_token_interno_e_401(face_client, metodo, url):
    corpo = {'json': {'personId': 'usuario:alvo@x.com', 'imageBase64': 'x'}} if metodo == 'post' else {}
    assert getattr(face_client, metodo)(url, **corpo).status_code == 401
    errado = getattr(face_client, metodo)(url, headers={'X-Internal-Token': 'errado'}, **corpo)
    assert errado.status_code == 401


def test_face_sem_token_nao_apaga_o_cadastro(face_client):
    from FaceAuth import data_manager
    face_client.delete('/face/usuario:alvo@x.com')
    assert data_manager.esta_cadastrado('usuario:alvo@x.com')


def test_face_com_token_interno_funciona(face_client):
    h = {'X-Internal-Token': settings.INTERNAL_TOKEN}
    assert face_client.get('/face/status/usuario:alvo@x.com', headers=h).json()['cadastrado'] is True
    assert face_client.delete('/face/usuario:alvo@x.com', headers=h).status_code == 200
    assert face_client.get('/face/status/usuario:alvo@x.com', headers=h).json()['cadastrado'] is False
    assert face_client.delete('/face/usuario:alvo@x.com', headers=h).status_code == 404


# ---------- JWT validado pela Web API ----------

class _FakeAsyncClient:
    chamadas = 0
    status = 200
    erro = False

    def __init__(self, *a, **k):
        pass

    async def __aenter__(self):
        return self

    async def __aexit__(self, *a):
        return False

    async def get(self, url, headers=None):
        import httpx
        type(self).chamadas += 1
        if type(self).erro:
            raise httpx.ConnectError('fora do ar')
        return httpx.Response(type(self).status, request=httpx.Request('GET', url))


@pytest.fixture
def api_dotnet(monkeypatch):
    _FakeAsyncClient.chamadas, _FakeAsyncClient.status, _FakeAsyncClient.erro = 0, 200, False
    monkeypatch.setattr(auth.httpx, 'AsyncClient', _FakeAsyncClient)
    return _FakeAsyncClient


def _app_com_rota_protegida():
    app = FastAPI()

    @app.get('/protegido', dependencies=[Depends(auth.exigir_usuario)])
    def protegido():
        return {'ok': True}

    return TestClient(app)


def test_sem_bearer_e_401(api_dotnet):
    assert _app_com_rota_protegida().get('/protegido').status_code == 401
    assert api_dotnet.chamadas == 0


def test_bearer_valido_passa_e_fica_em_cache(api_dotnet):
    c = _app_com_rota_protegida()
    h = {'Authorization': 'Bearer abc'}
    assert c.get('/protegido', headers=h).status_code == 200
    assert c.get('/protegido', headers=h).status_code == 200
    assert api_dotnet.chamadas == 1  # a segunda veio do cache


def test_bearer_rejeitado_pela_web_api_e_401_e_nao_entra_em_cache(api_dotnet):
    api_dotnet.status = 401
    c = _app_com_rota_protegida()
    h = {'Authorization': 'Bearer ruim'}
    assert c.get('/protegido', headers=h).status_code == 401
    assert c.get('/protegido', headers=h).status_code == 401
    assert api_dotnet.chamadas == 2


def test_web_api_fora_do_ar_e_503(api_dotnet):
    api_dotnet.erro = True
    assert _app_com_rota_protegida().get('/protegido', headers={'Authorization': 'Bearer abc'}).status_code == 503


# ---------- ticket do stream ----------

def test_ticket_vale_uma_vez():
    ticket = auth.emitir_ticket_stream()['ticket']
    auth.consumir_ticket_stream(ticket)
    with pytest.raises(HTTPException) as e:
        auth.consumir_ticket_stream(ticket)
    assert e.value.status_code == 401


def test_ticket_expirado_ou_ausente_e_recusado():
    ticket = auth.emitir_ticket_stream()['ticket']
    auth._tickets_stream[ticket] = 0.0
    for valor in (ticket, None, 'inventado'):
        with pytest.raises(HTTPException) as e:
            auth.consumir_ticket_stream(valor)
        assert e.value.status_code == 401


# ---------- rotas de câmera ----------

@pytest.fixture
def cameras_client():
    from yolo_tesseract import router as cam
    cam._detections.clear()
    app = FastAPI()
    app.include_router(cam.router)
    app.include_router(cam.stream_router)
    return TestClient(app), cam


@pytest.mark.parametrize('metodo,url', [
    ('get', '/cameras'), ('post', '/start'), ('post', '/stop/0'),
    ('get', '/last'), ('get', '/test/0'), ('post', '/stream-ticket'),
])
def test_rotas_de_camera_exigem_login(cameras_client, metodo, url):
    client, _ = cameras_client
    corpo = {'json': {'camera': 0}} if url == '/start' else {}
    assert getattr(client, metodo)(url, **corpo).status_code == 401


def test_stream_sem_ticket_valido_e_401(cameras_client):
    client, _ = cameras_client
    assert client.get('/stream/0').status_code == 401
    assert client.get('/stream/0?ticket=inventado').status_code == 401


def test_last_filtra_por_since_e_camera_e_vem_do_mais_novo(cameras_client):
    client, cam = cameras_client
    client.app.dependency_overrides[auth.exigir_usuario] = lambda: None
    cam.registrar_deteccao({'camera': 0, 'plate': 'AAA1111', 'bbox': [0, 0, 1, 1]})
    time.sleep(0.03)  # relógio do Windows tem resolução de ~15 ms
    cam.registrar_deteccao({'camera': 1, 'plate': 'BBB2222', 'bbox': [0, 0, 1, 1]})
    time.sleep(0.03)
    cam.registrar_deteccao({'camera': 0, 'plate': 'CCC3333', 'bbox': [0, 0, 1, 1]})
    todas = client.get('/last').json()['detections']
    assert [d['plate'] for d in todas] == ['CCC3333', 'BBB2222', 'AAA1111']
    assert [d['plate'] for d in client.get('/last?camera=0').json()['detections']] == ['CCC3333', 'AAA1111']
    corte = todas[2]['timestamp']
    assert [d['plate'] for d in client.get(f'/last?since={corte}').json()['detections']] == ['CCC3333', 'BBB2222']


def test_last_nunca_inventa_placas(cameras_client):
    client, _ = cameras_client
    client.app.dependency_overrides[auth.exigir_usuario] = lambda: None
    assert client.get('/last').json()['detections'] == []


def test_lista_de_cameras_fica_em_cache_e_atualizar_forca_nova_busca(cameras_client, monkeypatch):
    client, cam = cameras_client
    client.app.dependency_overrides[auth.exigir_usuario] = lambda: None
    chamadas = []
    monkeypatch.setattr(cam, 'list_cameras', lambda n, ignorar=(): chamadas.append(n) or [0])
    monkeypatch.setattr(cam, '_lista_cache', None)

    assert client.get('/cameras').json()['cameras'] == [0]
    assert client.get('/cameras').json()['cameras'] == [0]
    assert len(chamadas) == 1                      # a segunda veio do cache
    client.get('/cameras?atualizar=true')
    assert len(chamadas) == 2
    assert chamadas[0] == cam.MAX_PROBE
