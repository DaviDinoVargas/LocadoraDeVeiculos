import cv2
import os
from .utils import ensure_dir

# Classes do modelo YOLO genérico (pré-treinado em COCO) que indicam um
# veículo inteiro, não a placa em si. Como não existe um modelo YOLO
# treinado especificamente para placas neste projeto, tratamos essas
# classes como candidatas e recortamos heuristicamente a região inferior
# central da caixa (onde a placa costuma estar) em vez de mandar o
# veículo inteiro para o OCR.
VEHICLE_CLASSES = {'car', 'truck', 'bus', 'motorcycle', 'vehicle'}
PLATE_CLASSES = {'plate', 'license_plate', 'license-plate'}


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


def _regiao_provavel_da_placa(box):
    """Estima a sub-região de uma caixa de veículo onde a placa costuma
    ficar: faixa horizontal central, no terço inferior da caixa."""
    x1, y1, x2, y2 = box
    w = x2 - x1
    h = y2 - y1
    faixa_y1 = y1 + int(h * 0.65)
    faixa_y2 = y1 + int(h * 0.95)
    faixa_x1 = x1 + int(w * 0.15)
    faixa_x2 = x1 + int(w * 0.85)
    return (faixa_x1, faixa_y1, faixa_x2, faixa_y2)


def find_plate_candidates(detections):
    candidates = []
    for d in detections:
        label = d.get('label', '').lower()
        x1, y1, x2, y2 = d['box']
        w = x2 - x1
        h = y2 - y1

        if any(p in label for p in PLATE_CLASSES):
            candidates.append(d)
            continue

        if any(v in label for v in VEHICLE_CLASSES):
            candidato = dict(d)
            candidato['box'] = _regiao_provavel_da_placa(d['box'])
            candidato['_heuristic'] = True
            candidates.append(candidato)
            continue

        # fallback: nenhuma classe reconhecida, usa proporção larga
        # (mais largo que alto) como indício fraco de placa
        aspect = w / (h + 1e-6)
        if aspect > 2.0 and w * h > 500:
            candidato = dict(d)
            candidato['_heuristic'] = True
            candidates.append(candidato)
    return candidates


def save_crop(crop, out_dir, prefix='crop'):
    ensure_dir(out_dir)
    idx = len(os.listdir(out_dir))
    fname = os.path.join(out_dir, f"{prefix}_{idx}.jpg")
    cv2.imwrite(fname, crop)
    return fname
