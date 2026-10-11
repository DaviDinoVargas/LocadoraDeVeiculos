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
