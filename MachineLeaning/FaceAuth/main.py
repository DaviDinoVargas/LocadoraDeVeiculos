# Demonstração local (fora da API) do pipeline de reconhecimento facial:
# abre a webcam, desenha a malha facial do MediaPipe e mostra em uma janela
# do OpenCV. Serve para validar a câmera/modelo na máquina do usuário; o
# fluxo real de cadastro/verificação usado pelo Angular é o de
# FaceAuth/router.py (endpoints /face/enroll e /face/verify).
import cv2
import numpy as np

from face_recognition import FaceRecognition

if __name__ == "__main__":
    reconhecedor = FaceRecognition()
    print("Pressione 'q' na janela de vídeo para sair.")

    def on_frame(imagem_pil):
        frame = cv2.cvtColor(np.array(imagem_pil), cv2.COLOR_RGB2BGR)
        cv2.imshow("FaceAuth - pressione 'q' para sair", frame)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            reconhecedor.stop()

    try:
        reconhecedor.run_face_mesh(on_frame=on_frame)
    finally:
        cv2.destroyAllWindows()
