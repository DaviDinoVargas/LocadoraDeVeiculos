"""Loop de processamento do YOLO/OCR (yolo_tesseract/service.py), com detector,
OCR e câmera falsos. Foco: um frame ruim não pode matar a thread (antes, a
primeira exceção do OCR encerrava o monitoramento em silêncio)."""
import os
import sys
import threading
import time

import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from shared.camera_manager import CameraHub
from yolo_tesseract import service as service_mod
from yolo_tesseract.service import YoloTessService


class _Cap:
    def isOpened(self):
        return True

    def read(self):
        time.sleep(0.005)
        return True, np.zeros((200, 400, 3), dtype=np.uint8)

    def release(self):
        pass


class _Detector:
    def predict(self, frame, conf=0.35):
        return [{'box': (0, 0, 400, 200), 'conf': 0.9, 'class': 2, 'label': 'car'}]


def _service(monkeypatch, ocr):
    monkeypatch.setattr(service_mod, 'ocr_disponivel', lambda: (True, 'fake'))
    monkeypatch.setattr(service_mod, 'ocr_plate_image', ocr)
    svc = YoloTessService.__new__(YoloTessService)
    svc.hub = CameraHub(lambda i: _Cap())
    svc.detector = _Detector()
    svc.running = {}
    svc.lock = threading.Lock()
    return svc


def _esperar(cond, timeout=3.0):
    fim = time.time() + timeout
    while time.time() < fim:
        if cond():
            return True
        time.sleep(0.02)
    return False


def test_erro_do_ocr_em_um_frame_nao_derruba_a_thread(monkeypatch):
    chamadas = {'n': 0}

    def ocr(_crop):
        chamadas['n'] += 1
        if chamadas['n'] <= 2:
            raise RuntimeError('falha pontual do OCR')
        return 'ABC1D23'

    svc = _service(monkeypatch, ocr)
    achadas = []
    ok, motivo = svc.start_camera_processing(0, on_plate_callback=achadas.append, max_fps=100)
    assert ok, motivo
    assert _esperar(lambda: achadas), 'a thread morreu depois do primeiro erro'
    assert achadas[0]['plate'] == 'ABC1D23' and achadas[0]['camera'] == 0
    assert svc.stop_camera(0)
    assert _esperar(lambda: 0 not in svc.running)
    assert not svc.hub.esta_aberta(0)  # liberou a câmera ao parar


def test_mesma_placa_nao_repete_dentro_da_janela(monkeypatch):
    svc = _service(monkeypatch, lambda _c: 'ABC1234')
    achadas = []
    svc.start_camera_processing(0, on_plate_callback=achadas.append, max_fps=100)
    time.sleep(0.5)
    svc.stop_camera(0)
    assert len(achadas) == 1


def test_start_recusa_com_motivo_quando_ocr_indisponivel(monkeypatch):
    svc = _service(monkeypatch, lambda _c: None)
    monkeypatch.setattr(service_mod, 'ocr_disponivel', lambda: (False, 'Binário do Tesseract não encontrado'))
    ok, motivo = svc.start_camera_processing(0, max_fps=10)
    assert not ok and 'Tesseract' in motivo
    assert not svc.hub.esta_aberta(0)


def test_start_recusa_camera_que_nao_abre(monkeypatch):
    svc = _service(monkeypatch, lambda _c: None)
    svc.hub = CameraHub(lambda i: None)
    ok, motivo = svc.start_camera_processing(7, max_fps=10)
    assert not ok and '7' in motivo


# ---- ponta a ponta com cenas sintéticas: detector falso + localizador + OCR de verdade ----
import pytest  # noqa: E402
from cenas_placa import cena_com_carro, desenhar_placa  # noqa: E402
from yolo_tesseract.tesseract_service import ocr_disponivel as _ocr_ok  # noqa: E402

precisa_tesseract = pytest.mark.skipif(not _ocr_ok()[0], reason='Tesseract não instalado')


class _CapCena:
    def __init__(self, frame):
        self._frame = frame

    def isOpened(self):
        return True

    def read(self):
        time.sleep(0.01)
        return True, self._frame.copy()

    def release(self):
        pass


class _DetectorFixo:
    def __init__(self, dets):
        self._dets = dets

    def predict(self, frame, conf=0.35):
        return self._dets


def _servico_com_cena(frame, dets):
    svc = YoloTessService.__new__(YoloTessService)
    svc.hub = CameraHub(lambda i: _CapCena(frame))
    svc.detector = _DetectorFixo(dets)
    svc.running = {}
    svc.lock = threading.Lock()
    return svc


def _ler_placas(svc, segundos=6.0):
    achadas = []
    ok, motivo = svc.start_camera_processing(0, on_plate_callback=achadas.append, max_fps=20)
    assert ok, motivo
    _esperar(lambda: achadas, timeout=segundos)
    svc.stop_camera(0)
    _esperar(lambda: 0 not in svc.running)
    return achadas


@precisa_tesseract
def test_le_placa_pequena_dentro_do_carro_detectado():
    frame, carro, _ = cena_com_carro('BRA2E19', largura_placa=110, desfoque=1.0, ruido=5)
    svc = _servico_com_cena(frame, [{'box': carro, 'conf': 0.54, 'class': 2, 'label': 'car'}])

    achadas = _ler_placas(svc)

    assert achadas and achadas[0]['plate'] == 'BRA2E19'
    assert achadas[0]['confidence'] == 0.54


@precisa_tesseract
def test_le_placa_quando_o_yolo_nao_enxerga_nenhum_veiculo():
    # foto só da placa: o YOLO COCO não detecta nada, mas a placa ainda deve ser lida
    img = np.full((480, 640, 3), (110, 120, 125), np.uint8)
    placa = desenhar_placa('BRA2E19', 300)
    h, w = placa.shape[:2]
    img[200:200 + h, 150:150 + w] = placa
    svc = _servico_com_cena(img, [])

    achadas = _ler_placas(svc)

    assert achadas and achadas[0]['plate'] == 'BRA2E19'


def test_so_confirma_a_placa_depois_de_lida_mais_de_uma_vez(monkeypatch):
    svc = _service(monkeypatch, lambda _c: 'ABC1234')
    leituras, agora = {}, 100.0
    assert svc._confirmou(leituras, 'ABC1234', agora) is False
    assert svc._confirmou(leituras, 'ABC1234', agora + 0.2) is True
    # leitura isolada e antiga não conta
    assert svc._confirmou(leituras, 'XYZ9876', agora) is False
    assert svc._confirmou(leituras, 'XYZ9876', agora + 10) is False


# ---- câmera presa no servidor: libera sozinha sem contato do navegador ----

def test_libera_a_camera_quando_o_navegador_some(monkeypatch):
    monkeypatch.setattr(service_mod, 'LIMITE_OCIOSO_S', 0.3)
    svc = _service(monkeypatch, lambda _c: None)
    ok, motivo = svc.start_camera_processing(0, max_fps=50)
    assert ok, motivo
    assert svc.hub.esta_aberta(0)
    # ninguém chama registrar_contato(): como a aba fechada sem "Parar"
    assert _esperar(lambda: 0 not in svc.running and not svc.hub.esta_aberta(0), timeout=3.0)


def test_contato_do_navegador_mantem_a_camera_ligada(monkeypatch):
    monkeypatch.setattr(service_mod, 'LIMITE_OCIOSO_S', 0.4)
    svc = _service(monkeypatch, lambda _c: None)
    svc.start_camera_processing(0, max_fps=50)
    fim = time.time() + 1.2
    while time.time() < fim:
        svc.registrar_contato()      # polling de /last
        time.sleep(0.1)
    assert svc.hub.esta_aberta(0) and 0 in svc.running
    svc.stop_camera(0)
    assert _esperar(lambda: 0 not in svc.running)
