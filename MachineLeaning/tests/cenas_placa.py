"""Gera cenas sintéticas com uma placa brasileira para testar localização + OCR sem
câmera: um "carro" (retângulo escuro com para-brisa/faróis) com a placa na parte de
baixo, opcionalmente pequena, desfocada ou com ruído (simulando foto de tela de celular).
"""
import os

import cv2
import numpy as np
import pytest
from PIL import Image, ImageDraw, ImageFont

# Fontes tentadas em ordem (Windows, Linux/CI, macOS). Sem nenhuma, os testes que dependem
# da cena são pulados em vez de falhar por um motivo que não é do código testado.
FONTES = [
    'C:/Windows/Fonts/arialbd.ttf',
    '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
    '/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf',
    '/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf',
    '/System/Library/Fonts/Supplemental/Arial Bold.ttf',
]


def _fonte(tamanho):
    for caminho in FONTES:
        if os.path.exists(caminho):
            return ImageFont.truetype(caminho, tamanho)
    try:
        return ImageFont.load_default(tamanho)  # Pillow >= 10.1: fonte escalável
    except TypeError:
        pytest.skip('nenhuma fonte TrueType disponível para desenhar a placa de teste')


def desenhar_placa(texto, largura=360, mercosul=True):
    """Placa 400x130 mm (proporção ~3,1:1)."""
    altura = int(largura / 3.1)
    img = Image.new('RGB', (largura, altura), (245, 245, 245) if mercosul else (190, 190, 190))
    d = ImageDraw.Draw(img)
    d.rectangle([2, 2, largura - 3, altura - 3], outline=(20, 20, 20), width=max(2, largura // 90))
    topo = int(altura * 0.22)
    if mercosul:
        d.rectangle([4, 4, largura - 5, topo], fill=(0, 51, 153))
        d.text((largura // 2, topo // 2 + 2), 'BRASIL', font=_fonte(max(8, topo - 6)), fill=(255, 255, 255), anchor='mm')
    fonte = _fonte(int(altura * 0.58))
    d.text((largura // 2, int(altura * 0.62)), texto, font=fonte, fill=(15, 15, 15), anchor='mm')
    return cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)


def cena_com_carro(texto, largura_placa=160, mercosul=True, desfoque=0, ruido=0, tamanho=(640, 480), seed=1):
    """Devolve (frame, caixa_do_carro, caixa_da_placa)."""
    rng = np.random.default_rng(seed)
    w, h = tamanho
    frame = np.full((h, w, 3), (120, 125, 120), dtype=np.uint8)
    cv2.rectangle(frame, (0, int(h * 0.75)), (w, h), (90, 90, 90), -1)  # chão

    cx1, cy1, cx2, cy2 = int(w * 0.18), int(h * 0.15), int(w * 0.82), int(h * 0.85)
    cv2.rectangle(frame, (cx1, cy1), (cx2, cy2), (60, 40, 40), -1)               # carroceria
    cv2.rectangle(frame, (cx1 + 40, cy1 + 25), (cx2 - 40, cy1 + 130), (150, 150, 160), -1)  # para-brisa
    cv2.circle(frame, (cx1 + 55, cy2 - 120), 22, (230, 230, 200), -1)           # faróis
    cv2.circle(frame, (cx2 - 55, cy2 - 120), 22, (230, 230, 200), -1)
    cv2.rectangle(frame, (cx1 + 90, cy2 - 60), (cx2 - 90, cy2 - 40), (20, 20, 20), -1)  # grade

    placa = desenhar_placa(texto, largura=largura_placa, mercosul=mercosul)
    ph, pw = placa.shape[:2]
    px = (cx1 + cx2) // 2 - pw // 2
    py = cy2 - 38 - ph
    frame[py:py + ph, px:px + pw] = placa

    if desfoque:
        frame = cv2.GaussianBlur(frame, (0, 0), desfoque)
    if ruido:
        frame = np.clip(frame.astype(np.int16) + rng.normal(0, ruido, frame.shape), 0, 255).astype(np.uint8)
    return frame, (cx1, cy1, cx2, cy2), (px, py, px + pw, py + ph)
