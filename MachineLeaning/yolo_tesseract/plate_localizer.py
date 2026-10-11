import cv2
import numpy as np
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


# ---------------------------------------------------------------------------
# Localização da placa por visão clássica (sem modelo treinado).
#
# O recorte "terço inferior do carro" só acerta quando a placa está exatamente
# onde o chute manda; em foto de celular, carro de lado ou placa pequena ele erra
# e o OCR recebe lixo. Aqui a placa é procurada de verdade: um retângulo de
# proporção ~3:1 (400x130 mm) com muitos traços verticais (os caracteres) ou claro
# com texto escuro dentro.
# ---------------------------------------------------------------------------
PROPORCAO_MIN = 1.9
PROPORCAO_MAX = 6.5
LARGURA_MIN_PX = 36
ALTURA_MIN_PX = 12
MAX_LADO_ANALISE = 960   # reduz imagens grandes antes de analisar (velocidade)


def _iou(a, b):
    ax1, ay1, ax2, ay2 = a
    bx1, by1, bx2, by2 = b
    iw = max(0, min(ax2, bx2) - max(ax1, bx1))
    ih = max(0, min(ay2, by2) - max(ay1, by1))
    inter = iw * ih
    uniao = (ax2 - ax1) * (ay2 - ay1) + (bx2 - bx1) * (by2 - by1) - inter
    return inter / uniao if uniao else 0.0


def _pontuar(gray, sobel, x, y, w, h):
    """Quanto o retângulo parece uma placa (0..~3). 0 = descartar."""
    proporcao = w / float(h)
    if not (PROPORCAO_MIN <= proporcao <= PROPORCAO_MAX):
        return 0.0
    regiao = gray[y:y + h, x:x + w]
    if regiao.size == 0:
        return 0.0
    # Placa tem contraste interno forte (texto sobre fundo) e muitas bordas verticais.
    contraste = float(regiao.std()) / 64.0
    bordas = float(sobel[y:y + h, x:x + w].mean()) / 255.0
    if contraste < 0.25 or bordas < 0.05:
        return 0.0
    proximidade = 1.0 - min(abs(proporcao - 3.1) / 3.1, 1.0)   # 3,1 é a proporção real
    return proximidade + min(contraste, 1.0) + min(bordas * 4, 1.0)


def _candidatos_por_bordas(gray, sobel):
    h, w = gray.shape
    kw = max(9, w // 28)
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (kw, max(3, kw // 4)))
    _, bin_ = cv2.threshold(sobel, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    fechado = cv2.morphologyEx(bin_, cv2.MORPH_CLOSE, kernel)
    fechado = cv2.erode(fechado, None, iterations=1)
    fechado = cv2.dilate(fechado, None, iterations=1)
    contornos, _ = cv2.findContours(fechado, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    return [cv2.boundingRect(c) for c in contornos]


def _candidatos_por_retangulo_claro(gray):
    suave = cv2.GaussianBlur(gray, (5, 5), 0)
    _, bin_ = cv2.threshold(suave, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (5, 5))
    bin_ = cv2.morphologyEx(bin_, cv2.MORPH_CLOSE, kernel)
    contornos, _ = cv2.findContours(bin_, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    saida = []
    for c in contornos:
        x, y, w, h = cv2.boundingRect(c)
        if w * h and cv2.contourArea(c) / float(w * h) > 0.7:   # bem retangular
            saida.append((x, y, w, h))
    return saida


def find_plate_regions(frame, box=None, max_regions=3, margem=0.06):
    """Procura retângulos com cara de placa em `frame` (ou só dentro de `box`).
    Devolve até `max_regions` caixas (x1, y1, x2, y2) em coordenadas do frame,
    da mais provável para a menos provável."""
    fh, fw = frame.shape[:2]
    if box is None:
        ox, oy, roi = 0, 0, frame
    else:
        bx1, by1, bx2, by2 = box
        ex, ey = int((bx2 - bx1) * 0.03), int((by2 - by1) * 0.03)
        ox, oy = max(0, bx1 - ex), max(0, by1 - ey)
        roi = frame[oy:min(fh, by2 + ey), ox:min(fw, bx2 + ex)]
    if roi.size == 0:
        return []

    escala = min(1.0, MAX_LADO_ANALISE / float(max(roi.shape[:2])))
    if escala < 1.0:
        roi = cv2.resize(roi, None, fx=escala, fy=escala, interpolation=cv2.INTER_AREA)

    gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
    sobel = cv2.convertScaleAbs(cv2.Sobel(cv2.GaussianBlur(gray, (3, 3), 0), cv2.CV_16S, 1, 0, ksize=3))

    pontuados = []
    for (x, y, w, h) in _candidatos_por_bordas(gray, sobel) + _candidatos_por_retangulo_claro(gray):
        if w < LARGURA_MIN_PX * escala or h < ALTURA_MIN_PX * escala:
            continue
        nota = _pontuar(gray, sobel, x, y, w, h)
        if nota > 0:
            pontuados.append((nota, (x, y, x + w, y + h)))

    pontuados.sort(key=lambda t: t[0], reverse=True)
    escolhidos = []
    for nota, caixa in pontuados:
        if all(_iou(caixa, outra) < 0.3 for _, outra in escolhidos):
            escolhidos.append((nota, caixa))
        if len(escolhidos) >= max_regions:
            break

    resultado = []
    for _, (x1, y1, x2, y2) in escolhidos:
        mx, my = int((x2 - x1) * margem), int((y2 - y1) * margem * 2)
        x1, y1 = max(0, x1 - mx), max(0, y1 - my)
        x2, y2 = min(roi.shape[1], x2 + mx), min(roi.shape[0], y2 + my)
        resultado.append((int(x1 / escala) + ox, int(y1 / escala) + oy, int(x2 / escala) + ox, int(y2 / escala) + oy))
    return resultado
