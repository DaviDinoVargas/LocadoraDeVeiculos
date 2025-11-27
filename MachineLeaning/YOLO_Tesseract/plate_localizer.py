import cv2
from utils import ensure_dir
import os




def crop_box(frame, box, margin=0.1):
    h, w = frame.shape[:2]
    x1, y1, x2, y2 = box
    bw = x2 - x1
    bh = y2 - y1
    mx = int(bw * margin)
    my = int(bh * margin)
    x1 = max(0, x1 - mx)
    y1 = max(0, y1 - my)
    x2 = min(w, x2 + mx)
    y2 = min(h, y2 + my)
    return frame[y1:y2, x1:x2]




def find_plate_candidates(detections):
# """
# Filtra detecções possivelmente relevantes para placa.
# Depende do seu modelo: se o YOLO tiver classe específica 'license_plate', priorize-a.
# Caso contrário, procura por veículo e depois aplica heurísticas (aspect ratio).
# """
# # Espera lista de {'box':(x1,y1,x2,y2), 'label': 'car'...}
    candidates = []
    for d in detections:
        label = d.get('label','') .lower()
        x1,y1,x2,y2 = d['box']
        w = x2 - x1
        h = y2 - y1
        if 'plate' in label or 'license' in label:
            candidates.append(d)
            continue
        # heurística: placas geralmente têm proporção larga (mais largas que altas)
        aspect = w / (h+1e-6)
        if aspect > 1.5 and w*h > 500:
        # pode ser placa ou carro visto de perto; adiciona como candidato com lower priority
            d['_heuristic'] = True
            candidates.append(d)
    return candidates

def save_crop(img, crop, out_dir, prefix='crop'):
    ensure_dir(out_dir)
    idx = len(os.listdir(out_dir))
    fname = os.path.join(out_dir, f"{prefix}_{idx}.jpg")
    cv2.imwrite(fname, crop)
    return fname