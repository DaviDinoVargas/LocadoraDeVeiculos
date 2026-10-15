"""Comportamento das rotas de câmera do ML (iniciar, parar, últimas detecções, teste de câmera e lista), com o serviço de leitura de placas e
o capturador de câmeras FALSOS. A autorização por empresa já é coberta em test_isolamento_cameras.py; aqui o que se confere é o que a rota
devolve e o que ela manda o serviço fazer.
"""
import os
import sys
import time
from types import SimpleNamespace

import numpy as np
import pytest
from fastapi import FastAPI
from fastapi.testclient import TestClient

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from shared import auth, limites
from yolo_tesseract import router as cam


class ServicoFalso:
    def __init__(self, resposta=(True, ''), para=True):
        self.resposta, self.para = resposta, para
        self.inicios, self.paradas, self.contatos = [], [], 0

    def start_camera_processing(self, camera, on_plate_callback=None, max_fps=10):
        self.inicios.append((camera, on_plate_callback, max_fps))
        return self.resposta

    def stop_camera(self, camera):
        self.paradas.append(camera)
        return self.para

    def registrar_contato(self):
        self.contatos += 1


class HubFalso:
    def __init__(self, abre=True, frame=None, abertas=()):
        self.abre, self.frame, self._abertas = abre, frame, list(abertas)
        self.liberadas = []

    def acquire(self, camera):
        return self.abre

    def read(self, camera, seq, timeout=1.0):
        return self.frame, seq + 1

    def release(self, camera):
        self.liberadas.append(camera)

    def abertas(self):
        return self._abertas


@pytest.fixture
def api(monkeypatch):
    limites.zerar()
    cam._detections.clear()
    app = FastAPI()
    app.dependency_overrides[auth.exigir_dono_das_cameras] = lambda: auth.Usuario(usuario_id='u1', empresa_id='e1', cargo='Empresa')
    app.include_router(cam.router)
    return TestClient(app)


# ---------------------------------------------------------------- iniciar ----------------------------------------------------

def test_iniciar_manda_o_servico_processar_a_camera_com_o_fps_pedido_e_o_callback_de_placas(api, monkeypatch):
    servico = ServicoFalso()
    monkeypatch.setattr(cam, 'service', servico)

    resposta = api.post('/start', json={'camera': 2, 'fps': 15})

    assert resposta.status_code == 200
    assert resposta.json() == {'ok': True, 'camera': 2, 'fps': 15.0}
    assert servico.inicios == [(2, cam.registrar_deteccao, 15.0)]


def test_iniciar_sem_o_modulo_de_placas_responde_503_com_o_motivo(api, monkeypatch):
    monkeypatch.setattr(cam, 'service', None)
    monkeypatch.setattr(cam, 'erro_yolo', 'modelo yolov8n.pt ausente')

    resposta = api.post('/start', json={'camera': 0})

    assert resposta.status_code == 503
    assert 'modelo yolov8n.pt ausente' in resposta.json()['detail']


@pytest.mark.parametrize('motivo,status', [
    ('Tesseract não encontrado no PATH', 503),     # OCR ausente é problema do servidor
    ('A câmera já está em processamento.', 409),   # o resto é estado/conflito
    ('Não foi possível abrir a câmera 3.', 409),
])
def test_iniciar_recusado_pelo_servico_vira_503_se_o_ocr_falta_e_409_nos_demais_casos(api, monkeypatch, motivo, status):
    monkeypatch.setattr(cam, 'service', ServicoFalso(resposta=(False, motivo)))

    resposta = api.post('/start', json={'camera': 3})

    assert resposta.status_code == status
    assert resposta.json()['detail'] == motivo


@pytest.mark.parametrize('fps', [0, -5, 31, 1000])
def test_iniciar_com_fps_fora_do_limite_responde_422_e_nao_toca_na_camera(api, monkeypatch, fps):
    servico = ServicoFalso()
    monkeypatch.setattr(cam, 'service', servico)

    resposta = api.post('/start', json={'camera': 0, 'fps': fps})

    assert resposta.status_code == 422
    assert servico.inicios == []


# ---------------------------------------------------------------- parar ------------------------------------------------------

def test_parar_camera_em_processamento_confirma_e_camera_parada_responde_404(api, monkeypatch):
    servico = ServicoFalso(para=True)
    monkeypatch.setattr(cam, 'service', servico)

    ok = api.post('/stop/1')
    servico.para = False
    nao_rodando = api.post('/stop/7')

    assert ok.status_code == 200 and ok.json() == {'ok': True, 'camera': 1}
    assert nao_rodando.status_code == 404
    assert servico.paradas == [1, 7]


def test_parar_sem_o_modulo_de_placas_responde_404(api, monkeypatch):
    monkeypatch.setattr(cam, 'service', None)

    assert api.post('/stop/0').status_code == 404


# ---------------------------------------------------------------- últimas detecções ------------------------------------------

def _detecoes(*placas_e_cameras):
    for placa, camera in placas_e_cameras:
        cam.registrar_deteccao({'camera': camera, 'plate': placa, 'bbox': [0, 0, 10, 10], 'confidence': 0.9})


def test_ultimas_deteccoes_vem_da_mais_nova_para_a_mais_antiga_e_respeitam_limit_e_camera(api, monkeypatch):
    monkeypatch.setattr(cam, 'service', ServicoFalso())
    _detecoes(('AAA1A11', 0), ('BBB2B22', 1), ('CCC3C33', 0), ('DDD4D44', 0))

    todas = api.get('/last').json()['detections']
    duas = api.get('/last', params={'limit': 2}).json()['detections']
    so_camera_1 = api.get('/last', params={'camera': 1}).json()['detections']
    nenhuma = api.get('/last', params={'limit': -3}).json()['detections']

    assert [d['plate'] for d in todas] == ['DDD4D44', 'CCC3C33', 'BBB2B22', 'AAA1A11']
    assert [d['plate'] for d in duas] == ['DDD4D44', 'CCC3C33']
    assert [d['plate'] for d in so_camera_1] == ['BBB2B22']
    assert nenhuma == []


def test_ultimas_deteccoes_com_since_devolve_so_as_posteriores_e_o_polling_avisa_que_alguem_acompanha(api, monkeypatch):
    servico = ServicoFalso()
    monkeypatch.setattr(cam, 'service', servico)
    relogio = iter([1000.0, 2000.0])
    # hora controlada só para o módulo da rota (patch em `time.time` global quebraria o resto): nada de depender de milissegundos
    monkeypatch.setattr(cam, 'time', SimpleNamespace(time=lambda: next(relogio), monotonic=time.monotonic))
    _detecoes(('AAA1A11', 0))
    _detecoes(('BBB2B22', 0))

    corpo = api.get('/last', params={'since': 1500.0}).json()

    assert [d['plate'] for d in corpo['detections']] == ['BBB2B22']
    assert servico.contatos == 1, 'cada consulta do navegador conta como contato, para o serviço não soltar a câmera por ociosidade'


def test_ultimas_deteccoes_informam_se_o_yolo_esta_disponivel(api, monkeypatch):
    monkeypatch.setattr(cam, 'service', None)
    monkeypatch.setattr(cam, 'HAS_YOLO', False)

    assert api.get('/last').json() == {'detections': [], 'yolo_available': False}


def test_o_historico_guarda_so_as_200_deteccoes_mais_recentes():
    cam._detections.clear()

    for i in range(cam.MAX_DETECCOES + 25):
        cam.registrar_deteccao({'camera': 0, 'plate': f'AAA{i:04d}'[:7]})

    assert cam.total_deteccoes() == cam.MAX_DETECCOES
    assert cam._detections[0]['plate'] == 'AAA0025', 'as mais antigas saem primeiro'


def test_registrar_deteccao_preenche_bbox_e_hora_quando_o_servico_nao_manda():
    cam._detections.clear()

    cam.registrar_deteccao({'plate': 'XYZ9Z99'})

    d = cam._detections[0]
    assert d['camera'] == 0 and d['bbox'] == [0, 0, 0, 0] and d['confidence'] is None and d['timestamp'] > 0


# ---------------------------------------------------------------- testar câmera -----------------------------------------------

def test_testar_camera_que_nao_abre_avisa_sem_segurar_nada(api, monkeypatch):
    hub = HubFalso(abre=False)
    monkeypatch.setattr(cam, 'hub', hub)

    corpo = api.get('/test/5').json()

    assert corpo['status'] == 'error' and 'abrir' in corpo['message']
    assert hub.liberadas == [], 'não abriu, então não há o que liberar'


def test_testar_camera_que_abre_mas_nao_entrega_frames_avisa_e_libera_a_camera(api, monkeypatch):
    hub = HubFalso(abre=True, frame=None)
    monkeypatch.setattr(cam, 'hub', hub)

    corpo = api.get('/test/5').json()

    assert corpo['status'] == 'error' and 'frames' in corpo['message']
    assert hub.liberadas == [5], 'a câmera precisa ser liberada mesmo quando a leitura falha (senão fica presa)'


def test_testar_camera_funcionando_informa_o_tamanho_do_frame_e_libera_a_camera(api, monkeypatch):
    hub = HubFalso(abre=True, frame=np.zeros((480, 640, 3), dtype=np.uint8))
    monkeypatch.setattr(cam, 'hub', hub)

    corpo = api.get('/test/0').json()

    assert corpo == {'status': 'working', 'camera': 0, 'message': 'Câmera funcionando', 'frame_size': '640x480'}
    assert hub.liberadas == [0]


# ---------------------------------------------------------------- listar câmeras ---------------------------------------------

def test_listar_cameras_junta_as_achadas_com_as_abertas_sem_repetir_e_ordenadas(api, monkeypatch):
    monkeypatch.setattr(cam, 'hub', HubFalso(abertas=[2, 0]))
    monkeypatch.setattr(cam, 'list_cameras', lambda maximo, ignorar=(): [1, 0])
    monkeypatch.setattr(cam, '_lista_cache', None)

    corpo = api.get('/cameras', params={'atualizar': True}).json()

    assert corpo == {'cameras': [0, 1, 2], 'count': 3}


def test_listar_cameras_usa_o_cache_e_atualizar_forca_nova_busca(api, monkeypatch):
    chamadas = []

    def buscar(maximo, ignorar=()):
        chamadas.append(1)
        return [0]

    monkeypatch.setattr(cam, 'hub', HubFalso())
    monkeypatch.setattr(cam, 'list_cameras', buscar)
    monkeypatch.setattr(cam, '_lista_cache', None)

    api.get('/cameras')
    api.get('/cameras')
    assert len(chamadas) == 1, 'a segunda consulta vem do cache (procurar abre cada índice e demora)'
    api.get('/cameras', params={'atualizar': True})
    assert len(chamadas) == 2


def test_listar_cameras_com_falha_na_busca_devolve_as_abertas_com_o_aviso(api, monkeypatch):
    def quebra(maximo, ignorar=()):
        raise RuntimeError('driver de vídeo travou')

    monkeypatch.setattr(cam, 'hub', HubFalso(abertas=[3]))
    monkeypatch.setattr(cam, 'list_cameras', quebra)
    monkeypatch.setattr(cam, '_lista_cache', None)

    corpo = api.get('/cameras', params={'atualizar': True}).json()

    assert corpo == {'cameras': [3], 'error': 'Falha ao procurar câmeras', 'count': 1}
    assert 'driver de vídeo' not in str(corpo), 'o detalhe interno do erro não vaza para o cliente'
