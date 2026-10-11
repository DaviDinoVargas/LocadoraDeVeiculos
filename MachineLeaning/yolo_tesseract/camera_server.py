# camera_server.py
import cv2


def list_cameras(max_probe=8, ignorar=()):
    """Lista câmeras disponíveis. Índices em `ignorar` (já abertos pelo hub)
    não são sondados -- abrir uma câmera em uso falharia e ela sumiria da lista."""
    available = []
    for i in range(max_probe):
        if i in ignorar:
            continue
        cap = cv2.VideoCapture(i)
        try:
            if cap is not None and cap.isOpened():
                ret, _ = cap.read()
                if ret:
                    available.append(i)
        finally:
            if cap is not None:
                cap.release()
    return available


def open_camera(idx, width=None, height=None):
    """Abre uma câmera com configurações opcionais"""
    cap = cv2.VideoCapture(idx)
    if width:
        cap.set(cv2.CAP_PROP_FRAME_WIDTH, width)
    if height:
        cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
    return cap
