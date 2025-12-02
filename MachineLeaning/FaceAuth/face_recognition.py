import cv2
import mediapipe as mp
import time
from PIL import Image

class FaceRecognition:
    def __init__(self, app):
        self.app = app
        self.face_landmarks_data = []
        self.temp_landmarks = []
        self.last_face_detection = time.time()
        self.cap = None
        self.running = False

    def run_face_mesh(self):
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
                    
                    self.app.frame_buffer = Image.fromarray(image).resize((400, 300))
                    self.app.root.event_generate("<<NewFrame>>")
        
        except Exception as e:
            print(f"Error: {e}")
        finally:
            self.stop()

    def stop(self):
        self.running = False
        if self.cap:
            self.cap.release()
            self.cap = None

    def compare_faces(self, new_data, saved_data):
        if not saved_data or not new_data:
            return False
        
        threshold = 0.02
        required_similarity = 0.9

        for stored_sample in saved_data:
            similar_points = 0
            if len(new_data) != len(stored_sample):
                continue
            for new_point, stored_point in zip(new_data, stored_sample):
                dx = new_point['x'] - stored_point['x']
                dy = new_point['y'] - stored_point['y']
                distance = (dx**2 + dy**2)**0.5
                if distance < threshold:
                    similar_points += 1
            if similar_points / len(new_data) >= required_similarity:
                return True
        return False

    def normalize_landmarks(self, landmarks):
        center_x = sum(lm['x'] for lm in landmarks) / len(landmarks)
        center_y = sum(lm['y'] for lm in landmarks) / len(landmarks)
        
        normalized = []
        for lm in landmarks:
            normalized.append({
                'x': lm['x'] - center_x,
                'y': lm['y'] - center_y,
                'z': lm['z']
            })
        return normalized