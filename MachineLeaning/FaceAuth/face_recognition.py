import cv2
import mediapipe as mp

# Modelo pré-treinado genérico (MediaPipe FaceMesh) usado como extrator de
# landmarks faciais. Não há treinamento próprio: a "identidade" de uma
# pessoa é representada pela geometria normalizada dos 468 pontos do rosto,
# e a verificação compara essa geometria com a amostra cadastrada.
_face_mesh_estatico = None


def _obter_face_mesh_estatico():
    global _face_mesh_estatico
    if _face_mesh_estatico is None:
        _face_mesh_estatico = mp.solutions.face_mesh.FaceMesh(
            static_image_mode=True,
            max_num_faces=1,
            refine_landmarks=True,
            min_detection_confidence=0.5,
        )
    return _face_mesh_estatico


def extrair_landmarks_de_imagem(image_bgr):
    """Extrai os landmarks faciais de uma única imagem estática (BGR).

    Retorna None se nenhum rosto for encontrado, ou a lista de pontos
    {x, y, z} (coordenadas normalizadas 0..1 pelo próprio MediaPipe).
    """
    face_mesh = _obter_face_mesh_estatico()
    image_rgb = cv2.cvtColor(image_bgr, cv2.COLOR_BGR2RGB)
    resultado = face_mesh.process(image_rgb)
    if not resultado.multi_face_landmarks:
        return None
    landmarks = resultado.multi_face_landmarks[0]
    return [{'x': lm.x, 'y': lm.y, 'z': lm.z} for lm in landmarks.landmark]


def normalizar_landmarks(landmarks):
    """Centraliza os landmarks no próprio centroide, tornando a comparação
    invariante à posição do rosto no quadro."""
    center_x = sum(lm['x'] for lm in landmarks) / len(landmarks)
    center_y = sum(lm['y'] for lm in landmarks) / len(landmarks)

    normalizado = []
    for lm in landmarks:
        normalizado.append({
            'x': lm['x'] - center_x,
            'y': lm['y'] - center_y,
            'z': lm['z'],
        })
    return normalizado


def comparar_faces(novos_landmarks, landmarks_salvos, threshold=0.02, similaridade_minima=0.9):
    """Compara landmarks (já normalizados) de duas amostras e retorna
    (bateu: bool, similaridade: float)."""
    if not landmarks_salvos or not novos_landmarks:
        return False, 0.0

    if len(novos_landmarks) != len(landmarks_salvos):
        return False, 0.0

    pontos_similares = 0
    for novo, salvo in zip(novos_landmarks, landmarks_salvos):
        dx = novo['x'] - salvo['x']
        dy = novo['y'] - salvo['y']
        distancia = (dx ** 2 + dy ** 2) ** 0.5
        if distancia < threshold:
            pontos_similares += 1

    similaridade = pontos_similares / len(novos_landmarks)
    return similaridade >= similaridade_minima, similaridade
