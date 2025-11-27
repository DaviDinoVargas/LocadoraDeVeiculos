import os


CAMERA_SERVER = os.environ.get('CAMERA_SERVER', None) #camera_server, colocar URL NÃO ESQUECER!!
YOLO_MODEL = os.environ.get('YOLO_MODEL', 'yolov8n.pt') # path do peso YOLO
PLATE_CLASSES = ['license_plate', 'car', 'vehicle', 'truck'] # classes usadas para filtrar (ajuste conforme modelo)
MIN_CONFIDENCE = float(os.environ.get('MIN_CONF', 0.35))
OCR_LANG = os.environ.get('OCR_LANG', 'eng')

# Threshold de IOU/area para considerar bbox de placa
MIN_PLATE_AREA = int(os.environ.get('MIN_PLATE_AREA', 500))

# CROPS SALVOS!! TIRAR FUTURAMENTE
SAVE_CROPS = os.environ.get('SAVE_CROPS', 'false').lower() in ('1','true','yes')
CROPS_DIR = os.environ.get('CROPS_DIR', 'crops')
# FastAPI settings
HOST = os.environ.get('HOST', '0.0.0.0')
PORT = int(os.environ.get('PORT', 8000))