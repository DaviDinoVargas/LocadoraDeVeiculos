"""Testa a heurística de localização de placa (yolo_tesseract/plate_localizer.py),
usada porque não existe um modelo YOLO treinado especificamente para placas --
só o genérico (COCO), que reconhece "car"/"truck" etc., não "license_plate".
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from yolo_tesseract.plate_localizer import crop_box, find_plate_candidates


def test_deteccao_de_placa_e_usada_direto_sem_heuristica():
    deteccoes = [{'label': 'license_plate', 'box': (10, 10, 50, 30), 'conf': 0.9}]

    candidatos = find_plate_candidates(deteccoes)

    assert len(candidatos) == 1
    assert candidatos[0]['box'] == (10, 10, 50, 30)
    assert '_heuristic' not in candidatos[0]


def test_deteccao_de_veiculo_recorta_regiao_inferior_central():
    # Carro de 200x100 (COCO reconhece "car", não a placa em si) -- a
    # heurística deveria recortar uma faixa bem menor, dentro da caixa
    # original, na parte de baixo (onde fica a placa na maioria dos carros).
    deteccoes = [{'label': 'car', 'box': (0, 0, 200, 100), 'conf': 0.8}]

    candidatos = find_plate_candidates(deteccoes)

    assert len(candidatos) == 1
    assert candidatos[0]['_heuristic'] is True
    x1, y1, x2, y2 = candidatos[0]['box']
    assert 0 <= x1 < x2 <= 200
    assert 0 <= y1 < y2 <= 100
    assert y1 > 50  # metade de baixo da caixa original
    assert (x2 - x1) < 200  # bem mais estreito que o carro inteiro


def test_objeto_nao_reconhecido_e_largo_vira_candidato_fraco():
    # Nenhuma classe reconhecida, mas a proporção larga é um indício fraco de placa
    deteccoes = [{'label': 'unknown', 'box': (0, 0, 100, 20), 'conf': 0.5}]

    candidatos = find_plate_candidates(deteccoes)

    assert len(candidatos) == 1
    assert candidatos[0]['_heuristic'] is True


def test_objeto_nao_reconhecido_e_nao_largo_e_descartado():
    deteccoes = [{'label': 'unknown', 'box': (0, 0, 20, 20), 'conf': 0.5}]

    candidatos = find_plate_candidates(deteccoes)

    assert candidatos == []


def test_crop_box_limita_a_margem_aos_limites_do_frame():
    frame = np.zeros((100, 100, 3), dtype=np.uint8)

    # Caixa já encostando na borda -- a margem de 10% não pode "vazar"
    # para fora do frame (índice negativo ou maior que o tamanho da imagem).
    recorte = crop_box(frame, (0, 0, 10, 10), margin=0.5)

    assert recorte.shape[0] <= 100
    assert recorte.shape[1] <= 100
    assert recorte.size > 0
