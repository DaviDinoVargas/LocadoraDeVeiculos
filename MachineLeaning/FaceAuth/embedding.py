"""Identidade facial por embedding (S33): SFace (OpenCV Zoo, Apache-2.0) no lugar da comparação por geometria de landmarks.

O SFace transforma um rosto alinhado em um vetor de 128 números; dois rostos da mesma pessoa dão vetores com cosseno alto.
O alinhamento usa os 5 pontos clássicos (olhos, nariz, cantos da boca), tirados dos landmarks do MediaPipe que o ML já extrai,
então nenhum detector extra é necessário. O arquivo do modelo NÃO está no git: `baixar_sface.py` baixa de um commit fixo e confere
o SHA-256 e o tamanho; aqui o hash é conferido de novo antes de carregar.

Medido no LFW (protocolo oficial de 6000 pares) em docs/S33_EMBEDDING_FACIAL.md.
"""
from __future__ import annotations

import hashlib
import os

import cv2
import numpy as np

NOME_DO_ARQUIVO = 'face_recognition_sface_2021dec.onnx'
SHA256_ESPERADO = '0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79'
TAMANHO_ESPERADO = 38_696_353
TAMANHO_DO_VETOR = 128

# Índices do MediaPipe FaceMesh (468 pontos) usados no alinhamento.
_OLHO_A = (33, 133)     # cantos do olho de um lado
_OLHO_B = (362, 263)    # cantos do olho do outro lado
_NARIZ = 1
_BOCA_A = 61
_BOCA_B = 291


class ReconhecimentoIndisponivel(RuntimeError):
    """Modelo ausente, adulterado ou ilegível. Quem chama deve falhar FECHADO (recusar), nunca liberar."""


def _sha256(caminho: str) -> str:
    h = hashlib.sha256()
    with open(caminho, 'rb') as f:
        for bloco in iter(lambda: f.read(1 << 20), b''):
            h.update(bloco)
    return h.hexdigest()


def cinco_pontos(landmarks: list[dict], largura: int, altura: int) -> np.ndarray:
    """Os 5 pontos (olho, olho, nariz, boca, boca) em pixels, ordenados da esquerda para a direita da imagem, como o SFace espera."""
    def ponto(i):
        return np.array([landmarks[i]['x'] * largura, landmarks[i]['y'] * altura], dtype=np.float32)

    olho_a = (ponto(_OLHO_A[0]) + ponto(_OLHO_A[1])) / 2
    olho_b = (ponto(_OLHO_B[0]) + ponto(_OLHO_B[1])) / 2
    olhos = sorted([olho_a, olho_b], key=lambda p: p[0])
    boca = sorted([ponto(_BOCA_A), ponto(_BOCA_B)], key=lambda p: p[0])

    return np.array([olhos[0], olhos[1], ponto(_NARIZ), boca[0], boca[1]], dtype=np.float32)


def linha_do_rosto(landmarks: list[dict], largura: int, altura: int) -> np.ndarray:
    """Linha [x, y, w, h, 5 pontos, confiança] no formato do detector YuNet, que é o que `alignCrop` recebe."""
    xs = [lm['x'] * largura for lm in landmarks]
    ys = [lm['y'] * altura for lm in landmarks]
    x0, y0 = min(xs), min(ys)
    linha = np.zeros((1, 15), dtype=np.float32)
    linha[0, 0:4] = [x0, y0, max(xs) - x0, max(ys) - y0]
    linha[0, 4:14] = cinco_pontos(landmarks, largura, altura).reshape(-1)
    linha[0, 14] = 1.0
    return linha


class ReconhecedorFacial:
    """Carrega o SFace uma vez (sob demanda) e calcula o vetor de um rosto."""

    def __init__(self, pasta: str, nome: str = NOME_DO_ARQUIVO, sha256: str = SHA256_ESPERADO, tamanho: int | None = TAMANHO_ESPERADO):
        self.caminho = os.path.join(pasta, nome)
        self._sha256 = sha256
        self._tamanho = tamanho
        self._modelo = None

    def carregar(self) -> None:
        if self._modelo is not None:
            return

        if not os.path.isfile(self.caminho):
            raise ReconhecimentoIndisponivel(f'Modelo de reconhecimento ausente: {os.path.basename(self.caminho)}. Rode FaceAuth/baixar_sface.py.')

        if self._tamanho is not None and os.path.getsize(self.caminho) != self._tamanho:
            raise ReconhecimentoIndisponivel('O modelo de reconhecimento não tem o tamanho esperado; recusado.')

        if _sha256(self.caminho) != self._sha256:
            raise ReconhecimentoIndisponivel('O modelo de reconhecimento não confere com o SHA-256 esperado; recusado.')

        try:
            self._modelo = cv2.FaceRecognizerSF.create(self.caminho, '')
        except Exception as ex:  # noqa: BLE001 - qualquer falha de leitura é indisponibilidade
            raise ReconhecimentoIndisponivel(f'Não foi possível carregar o modelo de reconhecimento: {type(ex).__name__}') from ex

    def vetor(self, imagem_bgr: np.ndarray, landmarks: list[dict]) -> list[float]:
        """Vetor de 128 números do rosto da imagem (já normalizado para comparar por cosseno)."""
        self.carregar()
        altura, largura = imagem_bgr.shape[:2]
        alinhado = self._modelo.alignCrop(imagem_bgr, linha_do_rosto(landmarks, largura, altura))
        bruto = self._modelo.feature(alinhado).reshape(-1).astype(np.float64)
        norma = np.linalg.norm(bruto)
        return (bruto / norma if norma > 0 else bruto).tolist()


def similaridade_cosseno(a: list[float], b: list[float]) -> float:
    """Cosseno entre dois vetores (1 = idênticos, 0 = sem relação). Vetores de tamanho diferente ou nulos nunca batem."""
    if len(a) != TAMANHO_DO_VETOR or len(b) != TAMANHO_DO_VETOR:
        return -1.0

    va, vb = np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64)
    na, nb = np.linalg.norm(va), np.linalg.norm(vb)
    if not np.isfinite(na) or not np.isfinite(nb) or na == 0 or nb == 0:
        return -1.0

    return float(np.dot(va, vb) / (na * nb))
