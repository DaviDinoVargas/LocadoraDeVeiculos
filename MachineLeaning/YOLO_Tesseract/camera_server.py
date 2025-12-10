# camera_server.py
import cv2

def list_cameras(max_probe=8):
    """Lista câmeras disponíveis"""
    available = []
    for i in range(max_probe):
        cap = cv2.VideoCapture(i)
        if not cap or not cap.isOpened():
            try:
                cap.release()
            except:
                pass
            continue
        ret, _ = cap.read()
        if ret:
            available.append(i)
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