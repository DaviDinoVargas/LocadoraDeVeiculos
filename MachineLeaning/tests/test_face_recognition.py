"""Testa a comparação geométrica de landmarks faciais (FaceAuth), sem
depender de câmera ou do modelo MediaPipe em si -- só a lógica pura de
normalização/comparação, que é o que decide se um login facial bate ou não.
"""
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from FaceAuth.face_recognition import comparar_faces, normalizar_landmarks


def _landmarks_quadrado(deslocamento_x=0.0, deslocamento_y=0.0):
    """4 pontos formando um quadrado, deslocados de (deslocamento_x, deslocamento_y)."""
    base = [
        {'x': 0.0, 'y': 0.0, 'z': 0.0},
        {'x': 1.0, 'y': 0.0, 'z': 0.0},
        {'x': 1.0, 'y': 1.0, 'z': 0.0},
        {'x': 0.0, 'y': 1.0, 'z': 0.0},
    ]
    return [{'x': p['x'] + deslocamento_x, 'y': p['y'] + deslocamento_y, 'z': p['z']} for p in base]


def test_normalizar_landmarks_centraliza_no_centroide():
    landmarks = _landmarks_quadrado(deslocamento_x=10.0, deslocamento_y=-5.0)

    normalizados = normalizar_landmarks(landmarks)

    centro_x = sum(p['x'] for p in normalizados) / len(normalizados)
    centro_y = sum(p['y'] for p in normalizados) / len(normalizados)
    assert abs(centro_x) < 1e-9
    assert abs(centro_y) < 1e-9


def test_comparar_faces_mesma_geometria_bate():
    landmarks_a = normalizar_landmarks(_landmarks_quadrado())
    landmarks_b = normalizar_landmarks(_landmarks_quadrado())

    bateu, similaridade = comparar_faces(landmarks_a, landmarks_b)

    assert bateu is True
    assert similaridade == 1.0


def test_comparar_faces_mesmo_rosto_deslocado_ainda_bate():
    # normalizar_landmarks já centraliza no centroide -- um rosto deslocado
    # no quadro (mas com a mesma geometria) tem que continuar batendo.
    landmarks_a = normalizar_landmarks(_landmarks_quadrado())
    landmarks_b = normalizar_landmarks(_landmarks_quadrado(deslocamento_x=50.0, deslocamento_y=30.0))

    bateu, similaridade = comparar_faces(landmarks_a, landmarks_b)

    assert bateu is True
    assert similaridade == 1.0


def test_comparar_faces_geometria_bem_diferente_nao_bate():
    landmarks_a = normalizar_landmarks(_landmarks_quadrado())
    landmarks_b = normalizar_landmarks([
        {'x': 0.0, 'y': 0.0, 'z': 0.0},
        {'x': 5.0, 'y': 0.0, 'z': 0.0},
        {'x': 5.0, 'y': 5.0, 'z': 0.0},
        {'x': 0.0, 'y': 5.0, 'z': 0.0},
    ])

    bateu, similaridade = comparar_faces(landmarks_a, landmarks_b)

    assert bateu is False
    assert similaridade < 0.9


def test_comparar_faces_sem_amostra_salva_nao_bate():
    landmarks_a = normalizar_landmarks(_landmarks_quadrado())

    bateu, similaridade = comparar_faces(landmarks_a, [])

    assert bateu is False
    assert similaridade == 0.0


def test_comparar_faces_quantidade_de_pontos_diferente_nao_bate():
    landmarks_a = normalizar_landmarks(_landmarks_quadrado())
    landmarks_b = landmarks_a[:-1]  # um ponto a menos

    bateu, similaridade = comparar_faces(landmarks_a, landmarks_b)

    assert bateu is False
    assert similaridade == 0.0
