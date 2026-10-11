import os

# Windows: o backend de câmera padrão do OpenCV (Media Foundation) liga o "hardware transforms" e
# pode levar de 10 a 60 s para abrir uma webcam USB. Desligar costuma abrir em ~1 s. Precisa ser
# definido ANTES de o cv2 ser importado (este __init__ roda antes dos módulos do pacote).
os.environ.setdefault('OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS', '0')
