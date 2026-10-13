"""Prova de vida passiva (S3): decide se a foto mostra uma pessoa de verdade ou uma foto/tela/máscara.

Usa dois modelos MiniFASNet do projeto Silent-Face-Anti-Spoofing (Minivision, Apache-2.0), em escalas diferentes do rosto, e
soma as probabilidades, como o projeto original faz. Os pesos NÃO estão no git: `baixar_modelo.py` baixa de um commit fixo e
confere o SHA-256; aqui o hash é conferido de novo antes de carregar, e o carregamento usa `weights_only=True` (só tensores,
nunca código arbitrário do arquivo).

Limites honestos (ver docs/S3_PROVA_DE_VIDA.md): é um modelo de pesquisa de 2020, treinado com câmeras e cenários específicos;
reduz muito o ataque com foto impressa ou na tela do celular, mas não substitui um teste de ataque com a câmera real.
"""
from __future__ import annotations

import hashlib
import os
from dataclasses import dataclass

import cv2
import numpy as np
import torch
import torch.nn.functional as F

from .minifasnet import MiniFASNetV1SE, MiniFASNetV2

# nome do arquivo -> (SHA-256 esperado, construtor). O nome carrega a escala do recorte e o tamanho de entrada
# (padrão do projeto original: "<escala>_<alt>x<larg>_<modelo>.pth").
MODELOS = {
    '2.7_80x80_MiniFASNetV2.pth': ('a5eb02e1843f19b5386b953cc4c9f011c3f985d0ee2bb9819eea9a142099bec0', MiniFASNetV2),
    '4_0_0_80x80_MiniFASNetV1SE.pth': ('84ee1d37d96894d5e82de5a57df044ef80a58be2b218b5ed7cdfd875ec2f5990', MiniFASNetV1SE),
}

CLASSE_REAL = 1   # saídas do modelo: 0 e 2 = ataque (foto impressa / tela), 1 = rosto real


class ProvaDeVidaIndisponivel(RuntimeError):
    """Pesos ausentes, adulterados ou ilegíveis. Quem chama deve falhar FECHADO (recusar), nunca liberar."""


@dataclass(frozen=True)
class ResultadoDeVida:
    vivo: bool
    pontuacao: float   # probabilidade média de "rosto real" entre os dois modelos, de 0 a 1


def _escala_e_tamanho(nome: str) -> tuple[float, int, int]:
    partes = nome.split('_')[:-1]
    altura, largura = (int(v) for v in partes[-1].split('x'))
    return float(partes[0]), altura, largura


def _nova_caixa(largura_img: int, altura_img: int, caixa, escala: float) -> tuple[int, int, int, int]:
    """Aumenta a caixa do rosto pela escala, sem sair da imagem (mesma regra do projeto original)."""
    x, y, bw, bh = caixa
    escala = min((altura_img - 1) / bh, min((largura_img - 1) / bw, escala))
    nova_l, nova_a = bw * escala, bh * escala
    cx, cy = bw / 2 + x, bh / 2 + y
    esq, topo = cx - nova_l / 2, cy - nova_a / 2
    dir_, base = cx + nova_l / 2, cy + nova_a / 2

    if esq < 0:
        dir_ -= esq
        esq = 0
    if topo < 0:
        base -= topo
        topo = 0
    if dir_ > largura_img - 1:
        esq -= dir_ - largura_img + 1
        dir_ = largura_img - 1
    if base > altura_img - 1:
        topo -= base - altura_img + 1
        base = altura_img - 1

    return int(esq), int(topo), int(dir_), int(base)


def recortar(imagem_bgr: np.ndarray, caixa, escala: float, altura: int, largura: int) -> np.ndarray:
    """Recorte do rosto com contexto ao redor, redimensionado para a entrada do modelo."""
    altura_img, largura_img = imagem_bgr.shape[:2]
    esq, topo, dir_, base = _nova_caixa(largura_img, altura_img, caixa, escala)
    return cv2.resize(imagem_bgr[topo:base + 1, esq:dir_ + 1], (largura, altura))


def sha256_do_arquivo(caminho: str) -> str:
    h = hashlib.sha256()
    with open(caminho, 'rb') as f:
        for bloco in iter(lambda: f.read(1 << 20), b''):
            h.update(bloco)
    return h.hexdigest()


class ProvaDeVida:
    """Carrega os modelos uma vez (sob demanda) e avalia imagens. Seguro para chamar de várias threads (só leitura)."""

    def __init__(self, pasta: str, limiar: float = 0.5, modelos: dict | None = None):
        self.pasta = pasta
        self.limiar = limiar
        self._definicao = modelos if modelos is not None else MODELOS
        self._modelos: list[tuple[float, int, int, torch.nn.Module]] | None = None

    def carregar(self) -> None:
        if self._modelos is not None:
            return

        carregados = []
        for nome, (hash_esperado, construtor) in self._definicao.items():
            caminho = os.path.join(self.pasta, nome)
            if not os.path.isfile(caminho):
                raise ProvaDeVidaIndisponivel(f'Modelo de prova de vida ausente: {nome}. Rode FaceAuth/antispoof/baixar_modelo.py.')

            if sha256_do_arquivo(caminho) != hash_esperado:
                raise ProvaDeVidaIndisponivel(f'O arquivo {nome} não confere com o SHA-256 esperado; recusado.')

            escala, altura, largura = _escala_e_tamanho(nome)
            kernel = ((altura + 15) // 16, (largura + 15) // 16)
            modelo = construtor(conv6_kernel=kernel)

            try:
                # weights_only=True: o arquivo só pode conter tensores; qualquer outro objeto serializado é recusado.
                estado = torch.load(caminho, map_location='cpu', weights_only=True)
                # treinados com DataParallel: as chaves vêm com o prefixo "module."
                estado = {(k[7:] if k.startswith('module.') else k): v for k, v in estado.items()}
                modelo.load_state_dict(estado)
            except Exception as ex:  # noqa: BLE001 - qualquer falha de leitura é indisponibilidade
                raise ProvaDeVidaIndisponivel(f'Não foi possível carregar {nome}: {type(ex).__name__}') from ex

            modelo.eval()
            carregados.append((escala, altura, largura, modelo))

        self._modelos = carregados

    def avaliar(self, imagem_bgr: np.ndarray, caixa) -> ResultadoDeVida:
        """`caixa` = [x, y, largura, altura] do rosto em pixels da imagem."""
        self.carregar()
        soma = np.zeros(3, dtype=np.float64)

        with torch.no_grad():
            for escala, altura, largura, modelo in self._modelos:
                recorte = recortar(imagem_bgr, caixa, escala, altura, largura)
                # entrada BGR em escala 0..255, sem normalizar: é como os pesos foram treinados
                tensor = torch.from_numpy(recorte.transpose((2, 0, 1))).float().unsqueeze(0)
                soma += F.softmax(modelo(tensor), dim=1)[0].numpy()

        pontuacao = float(soma[CLASSE_REAL] / len(self._modelos))
        return ResultadoDeVida(vivo=int(np.argmax(soma)) == CLASSE_REAL and pontuacao >= self.limiar, pontuacao=round(pontuacao, 4))


def caixa_dos_landmarks(landmarks: list[dict], largura: int, altura: int, margem: float = 0.0) -> list[int]:
    """Caixa [x, y, largura, altura] em pixels a partir dos landmarks normalizados (0..1) do MediaPipe."""
    xs = [lm['x'] for lm in landmarks]
    ys = [lm['y'] for lm in landmarks]
    x0, x1 = min(xs) * largura, max(xs) * largura
    y0, y1 = min(ys) * altura, max(ys) * altura
    bw, bh = x1 - x0, y1 - y0
    x0 -= bw * margem
    y0 -= bh * margem
    bw *= 1 + 2 * margem
    bh *= 1 + 2 * margem
    return [int(max(x0, 0)), int(max(y0, 0)), max(int(bw), 2), max(int(bh), 2)]
