import base64
import io
from typing import Annotated

import cv2
import numpy as np
from fastapi import APIRouter, Depends, HTTPException
from PIL import Image
from pydantic import BaseModel, Field

from shared import limites, settings
from shared.auth import exigir_token_interno

from .antispoof.liveness import ProvaDeVida, ProvaDeVidaIndisponivel, caixa_dos_landmarks
from .embedding import TAMANHO_DO_VETOR, ReconhecedorFacial, ReconhecimentoIndisponivel, similaridade_cosseno
from .face_recognition import extrair_landmarks_de_imagem

# Todo o módulo facial é só para o backend .NET (servidor-a-servidor): exige o
# X-Internal-Token (segredo compartilhado, ver shared/auth.py). O navegador nunca
# o recebe. O Angular fala com a Web API (/api/auth/rosto...), que valida o JWT,
# descobre quem é o usuário logado e só então chama este serviço.
#
# O serviço NÃO GUARDA NADA: dado biométrico fica cifrado no banco da Web API (S1).
# Aqui só se calcula: imagem -> vetor do rosto (cadastro) e imagem x vetores -> match (verificação).
# O vetor é um embedding de 128 números (SFace, S33); a comparação é por cosseno. Nada disso é registrado em log.
# O limite protege a CPU do ML (MediaPipe + modelos) mesmo de quem tem o token interno; chave = IP de origem (a Web API).
router = APIRouter(prefix="/face", tags=["face"],
                   dependencies=[Depends(exigir_token_interno), Depends(limites.limitar("face", maximo=120, janela_s=60))])

# Teto defensivo: a Web API guarda no máximo 3 amostras por pessoa.
MAXIMO_DE_AMOSTRAS = 10

# Uma foto de webcam (640x480, JPEG) tem ~100 KB; 4 milhões de caracteres de base64 (~3 MB) já é folga
# de sobra e impede que o corpo da requisição vire um jeito de esgotar a memória (S9).
MAXIMO_BASE64_CHARS = 4_000_000

# Cada amostra tem 128 números; o teto folgado evita que uma amostra gigante vire consumo de memória.
MAXIMO_DE_NUMEROS_POR_AMOSTRA = 2048


class ImagemFacialRequest(BaseModel):
    imageBase64: str = Field(max_length=MAXIMO_BASE64_CHARS)


class CompararRequest(BaseModel):
    imageBase64: str = Field(max_length=MAXIMO_BASE64_CHARS)
    samples: list[Annotated[list[float], Field(max_length=MAXIMO_DE_NUMEROS_POR_AMOSTRA)]] = Field(max_length=MAXIMO_DE_AMOSTRAS)


# Uma foto de webcam tem 0,3 megapixel; 16 megapixels (4000x4000) já cobrem qualquer câmera de celular. Um PNG de 136 KB pode declarar
# 144 milhões de pixels e custar mais de 1 GB de memória ao ser decodificado: o tamanho é lido do cabeçalho, sem decodificar (S9).
MAXIMO_DE_PIXELS = 16_000_000


def _conferir_tamanho_da_imagem(binario: bytes) -> None:
    try:
        with Image.open(io.BytesIO(binario)) as imagem:
            largura, altura = imagem.size
    except Image.DecompressionBombError:
        raise HTTPException(status_code=413, detail="Imagem grande demais")
    except Exception:
        raise HTTPException(status_code=400, detail="Não foi possível decodificar a imagem enviada")

    if largura * altura > MAXIMO_DE_PIXELS:
        raise HTTPException(status_code=413, detail="Imagem grande demais")


def _decodificar_imagem(image_base64: str):
    try:
        if ',' in image_base64:
            image_base64 = image_base64.split(',', 1)[1]
        binario = base64.b64decode(image_base64)
    except Exception:
        raise HTTPException(status_code=400, detail="Não foi possível decodificar a imagem enviada")

    _conferir_tamanho_da_imagem(binario)

    try:
        arr = np.frombuffer(binario, dtype=np.uint8)
        imagem = cv2.imdecode(arr, cv2.IMREAD_COLOR)
        if imagem is None:
            raise ValueError("imagem inválida")
        return imagem
    except Exception:
        raise HTTPException(status_code=400, detail="Não foi possível decodificar a imagem enviada")


_prova_de_vida: ProvaDeVida | None = None
_reconhecedor: ReconhecedorFacial | None = None


def _obter_prova_de_vida() -> ProvaDeVida:
    global _prova_de_vida
    if _prova_de_vida is None:
        _prova_de_vida = ProvaDeVida(settings.ANTISPOOF_DIR, settings.ANTISPOOF_LIMIAR)
    return _prova_de_vida


def _obter_reconhecedor() -> ReconhecedorFacial:
    global _reconhecedor
    if _reconhecedor is None:
        _reconhecedor = ReconhecedorFacial(settings.FACE_MODEL_DIR)
    return _reconhecedor


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


def _vetor_do_rosto(imagem, landmarks) -> list[float]:
    """Embedding de 128 números do rosto. Sem o modelo: 503, nunca cai para um método mais fraco."""
    try:
        return _obter_reconhecedor().vetor(imagem, landmarks)
    except ReconhecimentoIndisponivel:
        raise HTTPException(status_code=503, detail="Reconhecimento facial indisponível")


def _analisar(image_base64: str):
    imagem = _decodificar_imagem(image_base64)
    landmarks = extrair_landmarks_de_imagem(imagem)
    if landmarks is None:
        raise HTTPException(status_code=422, detail="Nenhum rosto detectado na imagem")
    return _vetor_do_rosto(imagem, landmarks), _avaliar_vida(imagem, landmarks)


@router.post("/landmarks")
def extrair_vetor(request: ImagemFacialRequest):
    """Calcula o vetor (embedding de 128 números) do rosto da foto, para a Web API cifrar e guardar. O nome da rota é
    histórico; o corpo devolve `vetor`. Só pode ser chamado pelo backend .NET (token interno no router)."""
    vetor, vida = _analisar(request.imageBase64)
    return {"vetor": vetor, **vida}


@router.post("/compare")
def comparar_com_amostras(request: CompararRequest):
    """Compara a foto enviada com os vetores que a Web API leu do banco (cosseno; basta bater com um). Os vetores existem em
    memória só durante esta chamada. Amostras que não têm 128 números (formato antigo) são ignoradas."""
    if not request.samples:
        raise HTTPException(status_code=400, detail="Nenhuma amostra para comparar")

    vetor, vida = _analisar(request.imageBase64)

    similaridades = [similaridade_cosseno(vetor, amostra) for amostra in request.samples if len(amostra) == TAMANHO_DO_VETOR]
    melhor = max(similaridades, default=0.0)

    # Foto que não parece uma pessoa de verdade nunca confirma identidade, mesmo que o vetor bata.
    bateu = melhor >= settings.FACE_COSINE_THRESHOLD and vida["vivo"]

    return {"match": bool(bateu), "confidence": round(max(melhor, 0.0), 4), **vida}
