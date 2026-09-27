import base64

import cv2
import numpy as np
from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from . import data_manager
from .face_recognition import comparar_faces, extrair_landmarks_de_imagem, normalizar_landmarks

router = APIRouter(prefix="/face", tags=["face"])

# Amostras suficientes para o rosto ser considerado uma correspondência.
# Uma única amostra combinando já é aceita (modo "ou": basta bater com
# qualquer uma das cadastradas), então o valor abaixo é só o piso de
# similaridade por amostra.
LIMIAR_DISTANCIA = 0.02
SIMILARIDADE_MINIMA = 0.9


class ImagemFacialRequest(BaseModel):
    personId: str
    imageBase64: str


def _decodificar_imagem(image_base64: str):
    try:
        if ',' in image_base64:
            image_base64 = image_base64.split(',', 1)[1]
        binario = base64.b64decode(image_base64)
        arr = np.frombuffer(binario, dtype=np.uint8)
        imagem = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        if imagem is None:
            raise ValueError("imagem inválida")
        return imagem
    except Exception:
        raise HTTPException(status_code=400, detail="Não foi possível decodificar a imagem enviada")


@router.post("/enroll")
def cadastrar_rosto(request: ImagemFacialRequest):
    """Cadastra uma amostra facial (landmarks do MediaPipe FaceMesh) para
    um personId. Chamar algumas vezes com ângulos levemente diferentes
    melhora a taxa de acerto na verificação."""
    imagem = _decodificar_imagem(request.imageBase64)
    landmarks = extrair_landmarks_de_imagem(imagem)
    if landmarks is None:
        raise HTTPException(status_code=422, detail="Nenhum rosto detectado na imagem")

    normalizados = normalizar_landmarks(landmarks)
    total_amostras = data_manager.salvar_amostra(request.personId, normalizados)
    return {"ok": True, "personId": request.personId, "amostras": total_amostras}


@router.post("/verify")
def verificar_rosto(request: ImagemFacialRequest):
    """Compara a imagem enviada com as amostras cadastradas do personId."""
    amostras_salvas = data_manager.carregar_amostras(request.personId)
    if not amostras_salvas:
        raise HTTPException(status_code=404, detail="Nenhum rosto cadastrado para este personId")

    imagem = _decodificar_imagem(request.imageBase64)
    landmarks = extrair_landmarks_de_imagem(imagem)
    if landmarks is None:
        raise HTTPException(status_code=422, detail="Nenhum rosto detectado na imagem")

    normalizados = normalizar_landmarks(landmarks)

    melhor_similaridade = 0.0
    bateu = False
    for amostra in amostras_salvas:
        match, similaridade = comparar_faces(
            normalizados, amostra,
            threshold=LIMIAR_DISTANCIA,
            similaridade_minima=SIMILARIDADE_MINIMA,
        )
        melhor_similaridade = max(melhor_similaridade, similaridade)
        if match:
            bateu = True

    return {"match": bateu, "confidence": round(melhor_similaridade, 4)}


@router.get("/status/{person_id}")
def status_cadastro(person_id: str):
    return {"personId": person_id, "cadastrado": data_manager.esta_cadastrado(person_id)}


@router.delete("/{person_id}")
def remover_cadastro(person_id: str):
    removido = data_manager.remover_pessoa(person_id)
    if not removido:
        raise HTTPException(status_code=404, detail="Nenhum rosto cadastrado para este personId")
    return {"ok": True}
