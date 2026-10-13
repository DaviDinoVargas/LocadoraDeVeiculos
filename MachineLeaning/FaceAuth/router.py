import base64

import cv2
import numpy as np
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from shared import limites, settings
from shared.auth import exigir_token_interno

from .antispoof.liveness import ProvaDeVida, ProvaDeVidaIndisponivel, caixa_dos_landmarks
from .face_recognition import comparar_faces, extrair_landmarks_de_imagem, normalizar_landmarks

# Todo o módulo facial é só para o backend .NET (servidor-a-servidor): exige o
# X-Internal-Token (segredo compartilhado, ver shared/auth.py). O navegador nunca
# o recebe. O Angular fala com a Web API (/api/auth/rosto...), que valida o JWT,
# descobre quem é o usuário logado e só então chama este serviço.
#
# O serviço NÃO GUARDA NADA: dado biométrico fica cifrado no banco da Web API (S1).
# Aqui só se calcula: imagem -> landmarks (cadastro) e imagem x amostras -> match (verificação).
# Nada disso é registrado em log.
# O limite protege a CPU do ML (MediaPipe) mesmo de quem tem o token interno; chave = IP de origem (a Web API).
router = APIRouter(prefix="/face", tags=["face"],
                   dependencies=[Depends(exigir_token_interno), Depends(limites.limitar("face", maximo=120, janela_s=60))])

# Uma única amostra combinando já é aceita (modo "ou": basta bater com qualquer uma das
# cadastradas), então o valor abaixo é só o piso de similaridade por amostra.
LIMIAR_DISTANCIA = 0.02
SIMILARIDADE_MINIMA = 0.9

# Teto defensivo: a Web API guarda no máximo 3 amostras por pessoa.
MAXIMO_DE_AMOSTRAS = 10


# Uma foto de webcam (640x480, JPEG) tem ~100 KB; 4 milhões de caracteres de base64 (~3 MB) já é folga
# de sobra e impede que o corpo da requisição vire um jeito de esgotar a memória (S9).
MAXIMO_BASE64_CHARS = 4_000_000


class ImagemFacialRequest(BaseModel):
    imageBase64: str = Field(max_length=MAXIMO_BASE64_CHARS)


class CompararRequest(BaseModel):
    imageBase64: str = Field(max_length=MAXIMO_BASE64_CHARS)
    samples: list[list[float]] = Field(max_length=MAXIMO_DE_AMOSTRAS)


def achatar_landmarks(landmarks) -> list[float]:
    """[{x,y,z}, ...] -> [x0,y0,z0,x1,y1,z1,...]: formato simples de guardar cifrado na Web API."""
    return [float(lm[eixo]) for lm in landmarks for eixo in ('x', 'y', 'z')]


def desachatar_landmarks(valores: list[float]):
    if len(valores) % 3 != 0:
        return []
    return [{'x': valores[i], 'y': valores[i + 1], 'z': valores[i + 2]} for i in range(0, len(valores), 3)]


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


_prova_de_vida: ProvaDeVida | None = None


def _obter_prova_de_vida() -> ProvaDeVida:
    global _prova_de_vida
    if _prova_de_vida is None:
        _prova_de_vida = ProvaDeVida(settings.ANTISPOOF_DIR, settings.ANTISPOOF_LIMIAR)
    return _prova_de_vida


def _avaliar_vida(imagem, landmarks) -> dict:
    """Prova de vida da foto (S3). Desligada: informa que não verificou. Ligada e sem os pesos: 503, nunca libera."""
    if not settings.ANTISPOOF_ATIVO:
        return {"provaDeVidaAtiva": False, "vivo": True, "pontuacaoDeVida": None}

    try:
        altura, largura = imagem.shape[:2]
        resultado = _obter_prova_de_vida().avaliar(imagem, caixa_dos_landmarks(landmarks, largura, altura))
    except ProvaDeVidaIndisponivel:
        raise HTTPException(status_code=503, detail="Prova de vida indisponível")

    return {"provaDeVidaAtiva": True, "vivo": resultado.vivo, "pontuacaoDeVida": resultado.pontuacao}


def _landmarks_normalizados(image_base64: str):
    imagem = _decodificar_imagem(image_base64)
    landmarks = extrair_landmarks_de_imagem(imagem)
    if landmarks is None:
        raise HTTPException(status_code=422, detail="Nenhum rosto detectado na imagem")
    return normalizar_landmarks(landmarks), _avaliar_vida(imagem, landmarks)


@router.post("/landmarks")
def extrair_landmarks(request: ImagemFacialRequest):
    """Extrai os landmarks normalizados (MediaPipe FaceMesh) de uma foto, para a Web API cifrar e
    guardar. Só pode ser chamado pelo backend .NET (token interno no router)."""
    normalizados, vida = _landmarks_normalizados(request.imageBase64)
    return {"landmarks": achatar_landmarks(normalizados), **vida}


@router.post("/compare")
def comparar_com_amostras(request: CompararRequest):
    """Compara a foto enviada com as amostras que a Web API leu do banco. As amostras existem em
    memória só durante esta chamada."""
    if not request.samples:
        raise HTTPException(status_code=400, detail="Nenhuma amostra para comparar")

    normalizados, vida = _landmarks_normalizados(request.imageBase64)

    melhor_similaridade = 0.0
    bateu = False
    for amostra in request.samples:
        match, similaridade = comparar_faces(
            normalizados, desachatar_landmarks(amostra),
            threshold=LIMIAR_DISTANCIA,
            similaridade_minima=SIMILARIDADE_MINIMA,
        )
        melhor_similaridade = max(melhor_similaridade, similaridade)
        if match:
            bateu = True

    # Foto que não parece uma pessoa de verdade nunca confirma identidade, mesmo que a geometria bata.
    return {"match": bateu and vida["vivo"], "confidence": round(melhor_similaridade, 4), **vida}
