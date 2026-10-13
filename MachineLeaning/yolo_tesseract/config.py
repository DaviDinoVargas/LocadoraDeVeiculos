import os


CAMERA_SERVER = os.environ.get('CAMERA_SERVER', None) #camera_server, colocar URL NÃO ESQUECER!!
YOLO_MODEL = os.environ.get('YOLO_MODEL', 'yolov8n.pt') # path do peso YOLO (COCO pré-treinado; ver plate_localizer.py)
MIN_CONFIDENCE = float(os.environ.get('MIN_CONF', 0.35))
OCR_LANG = os.environ.get('OCR_LANG', 'eng')
# Caminho do tesseract.exe quando ele não está no PATH (ex.: C:\Program Files\Tesseract-OCR\tesseract.exe)
TESSERACT_CMD = os.environ.get('TESSERACT_CMD') or None

# Threshold de IOU/area para considerar bbox de placa
MIN_PLATE_AREA = int(os.environ.get('MIN_PLATE_AREA', 500))

# CROPS SALVOS!! TIRAR FUTURAMENTE
SAVE_CROPS = os.environ.get('SAVE_CROPS', 'false').lower() in ('1','true','yes')
CROPS_DIR = os.environ.get('CROPS_DIR', 'crops')
# Recortes são imagens de pessoas e veículos: apagados depois deste prazo (S23).
CROPS_RETENTION_HOURS = float(os.environ.get('CROPS_RETENTION_HOURS', 24))
# SHA-256 do peso do YOLO; obrigatório fora de desenvolvimento (S23).
YOLO_MODEL_SHA256 = os.environ.get('YOLO_MODEL_SHA256') or None
# FastAPI settings
HOST = os.environ.get('HOST', '0.0.0.0')
PORT = int(os.environ.get('PORT', 8000))