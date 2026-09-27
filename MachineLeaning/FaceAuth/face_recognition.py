import cv2
import mediapipe as mp
import time
from PIL import Image

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


# --- Classe legada, usada apenas pela demonstração local via webcam (main.py) ---
class FaceRecognition:
    """Captura ao vivo de uma câmera local para testes manuais fora do
    fluxo web (não é usada pela API/Angular, que fazem enroll/verify por
    imagem única através de FaceAuth/router.py)."""

    def __init__(self):
        self.face_landmarks_data = []
        self.last_face_detection = time.time()
        self.cap = None
        self.running = False

    def run_face_mesh(self, on_frame=None):
        mp_drawing = mp.solutions.drawing_utils
        mp_drawing_styles = mp.solutions.drawing_styles
        mp_face_mesh = mp.solutions.face_mesh

        self.cap = cv2.VideoCapture(0)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)

        try:
            with mp_face_mesh.FaceMesh(
                max_num_faces=1,
                refine_landmarks=True,
                min_detection_confidence=0.9,
                min_tracking_confidence=0.9
            ) as face_mesh:
                self.running = True

                while self.running and self.cap.isOpened():
                    success, image = self.cap.read()
                    if not success:
                        continue

                    image = cv2.resize(image, (320, 240))
                    image = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
                    results = face_mesh.process(image)

                    has_face = False

                    if results.multi_face_landmarks:
                        has_face = True
                        for face_landmarks in results.multi_face_landmarks:
                            mp_drawing.draw_landmarks(
                                image=image,
                                landmark_list=face_landmarks,
                                connections=mp_face_mesh.FACEMESH_TESSELATION,
                                landmark_drawing_spec=None,
                                connection_drawing_spec=mp_drawing_styles
                                    .get_default_face_mesh_tesselation_style()
                            )
                            self.face_landmarks_data = [
                                {'x': lm.x, 'y': lm.y, 'z': lm.z} for lm in face_landmarks.landmark
                            ]
                    else:
                        self.face_landmarks_data = []

                    if has_face:
                        self.last_face_detection = time.time()
                    else:
                        if time.time() - self.last_face_detection > 1:
                            self.face_landmarks_data = []

                    if on_frame:
                        on_frame(Image.fromarray(image))

        except Exception as e:
            print(f"Error: {e}")
        finally:
            self.stop()

    def stop(self):
        self.running = False
        if self.cap:
            self.cap.release()
            self.cap = None
