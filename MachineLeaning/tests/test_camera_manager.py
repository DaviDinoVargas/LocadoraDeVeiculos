"""CameraHub: um único capturador por câmera, compartilhado entre stream e YOLO."""
import os
import sys
import threading
import time

import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from shared.camera_manager import CameraHub


class FakeCapture:
    def __init__(self, abre=True, frames=None, intervalo=0.005):
        self._abre = abre
        self._frames = frames  # None = infinitos
        self._enviados = 0
        self.liberada = False
        self._intervalo = intervalo

    def isOpened(self):
        return self._abre

    def read(self):
        time.sleep(self._intervalo)
        if self.liberada:
            return False, None
        if self._frames is not None and self._enviados >= self._frames:
            return False, None
        self._enviados += 1
        return True, np.full((4, 4, 3), self._enviados, dtype=np.uint8)

    def release(self):
        self.liberada = True


def _hub(**kw):
    capturas = []

    def abrir(indice):
        cap = FakeCapture(**kw)
        capturas.append(cap)
        return cap

    return CameraHub(abrir), capturas


def test_dois_consumidores_compartilham_uma_unica_captura():
    hub, capturas = _hub()
    assert hub.acquire(0) and hub.acquire(0)
    assert len(capturas) == 1
    assert hub.esta_aberta(0) and hub.abertas() == [0]
    hub.shutdown()


def test_camera_so_fecha_no_ultimo_release():
    hub, capturas = _hub()
    hub.acquire(0)
    hub.acquire(0)
    hub.release(0)
    assert hub.esta_aberta(0) and not capturas[0].liberada
    hub.release(0)
    assert not hub.esta_aberta(0)
    time.sleep(0.1)
    assert capturas[0].liberada


def test_acquire_falha_quando_camera_nao_abre():
    hub, _ = _hub(abre=False)
    assert hub.acquire(3) is False
    assert hub.abertas() == []


def test_read_entrega_frames_cada_vez_mais_novos():
    hub, _ = _hub()
    hub.acquire(0)
    f1, s1 = hub.read(0, 0, timeout=1.0)
    f2, s2 = hub.read(0, s1, timeout=1.0)
    assert f1 is not None and f2 is not None and s2 > s1
    hub.shutdown()


def test_read_de_camera_nao_aberta_devolve_none():
    hub, _ = _hub()
    assert hub.read(5, 0, timeout=0.05) == (None, 0)


def test_camera_que_para_de_responder_e_marcada_como_caida(monkeypatch):
    import shared.camera_manager as cm
    monkeypatch.setattr(cm, 'MAX_FALHAS_SEGUIDAS', 5)
    hub, capturas = _hub(frames=2)
    hub.acquire(0)
    time.sleep(0.4)
    assert not hub.esta_aberta(0)
    assert capturas[0].liberada
    # um novo acquire reabre em vez de reaproveitar a captura morta
    assert hub.acquire(0)
    assert len(capturas) == 2
    hub.shutdown()


def test_overlay_expira(monkeypatch):
    import shared.camera_manager as cm
    hub, _ = _hub()
    hub.acquire(0)
    hub.publicar_overlay(0, [{'box': (0, 0, 1, 1), 'label': 'car'}])
    assert hub.overlay(0)
    monkeypatch.setattr(cm, 'VALIDADE_OVERLAY_S', 0.0)
    time.sleep(0.05)  # relógio do Windows tem resolução de ~15 ms
    assert hub.overlay(0) == []
    hub.shutdown()


def test_acquire_concorrente_abre_uma_vez():
    hub, capturas = _hub()
    resultados = []
    ts = [threading.Thread(target=lambda: resultados.append(hub.acquire(0))) for _ in range(8)]
    [t.start() for t in ts]
    [t.join() for t in ts]
    assert all(resultados) and len(capturas) == 1
    hub.shutdown()
