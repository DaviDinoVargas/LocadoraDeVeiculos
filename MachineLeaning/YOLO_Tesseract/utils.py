import logging
import os

logging.basicConfig(level=logging.INFO, format='%(asctime)s [%(levelname)s] %(message)s')
log = logging.getLogger('yolo_tess')

def ensure_dir(path):
    os.makedirs(path, exist_ok=True)
    return path