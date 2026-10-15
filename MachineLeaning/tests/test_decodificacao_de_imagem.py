"""A imagem do rosto chega em base64 (com ou sem o prefixo `data:image/...;base64,`). Entrada inválida precisa virar 400 com mensagem
clara, nunca 500 nem a exceção interna."""
import base64
import os
import sys

import cv2
import numpy as np
import pytest
from fastapi import HTTPException

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from FaceAuth import router as face


def _png_base64(largura=8, altura=6, cor=(10, 200, 30)) -> str:
    imagem = np.zeros((altura, largura, 3), dtype=np.uint8)
    imagem[:] = cor
    ok, buffer = cv2.imencode('.png', imagem)
    assert ok
    return base64.b64encode(buffer.tobytes()).decode()


def test_decodifica_base64_puro_e_devolve_a_imagem_com_o_tamanho_e_as_cores_certas():
    imagem = face._decodificar_imagem(_png_base64(8, 6, (10, 200, 30)))

    assert imagem.shape == (6, 8, 3)
    assert tuple(imagem[0, 0]) == (10, 200, 30)


def test_decodifica_data_url_ignorando_o_prefixo():
    imagem = face._decodificar_imagem('data:image/png;base64,' + _png_base64(4, 4))

    assert imagem.shape == (4, 4, 3)


@pytest.mark.parametrize('entrada', ['', 'isto não é base64!!!', base64.b64encode(b'nao e uma imagem').decode(), 'data:image/png;base64,'])
def test_entrada_invalida_vira_400_com_mensagem_clara(entrada):
    with pytest.raises(HTTPException) as erro:
        face._decodificar_imagem(entrada)

    assert erro.value.status_code == 400
    assert erro.value.detail == 'Não foi possível decodificar a imagem enviada'

# ---------------------------------------------------------------- bomba de descompressão (S9)

def _png_com_dimensoes(largura: int, altura: int) -> bytes:
    """PNG válido de uma cor só: o arquivo é pequeno, mas declara `largura x altura` pixels."""
    import struct
    import zlib

    def bloco(tipo: bytes, dados: bytes) -> bytes:
        return struct.pack('>I', len(dados)) + tipo + dados + struct.pack('>I', zlib.crc32(tipo + dados) & 0xFFFFFFFF)

    comp = zlib.compressobj(9)
    linha = bytes(1 + largura)
    corpo = b''.join(comp.compress(linha) for _ in range(altura)) + comp.flush()
    return b'\x89PNG\r\n\x1a\n' + bloco(b'IHDR', struct.pack('>IIBBBBB', largura, altura, 8, 0, 0, 0, 0)) + bloco(b'IDAT', corpo) + bloco(b'IEND', b'')


def test_imagem_que_declara_pixels_demais_e_recusada_com_413_sem_decodificar(monkeypatch):
    bomba = _png_com_dimensoes(12000, 12000)          # ~130 KB de arquivo, 144 milhões de pixels
    chamadas = []
    monkeypatch.setattr(face.cv2, 'imdecode', lambda *a, **k: chamadas.append(a))

    with pytest.raises(HTTPException) as erro:
        face._decodificar_imagem(base64.b64encode(bomba).decode())

    assert len(bomba) < 1_000_000, 'o ataque é justamente o arquivo pequeno'
    assert erro.value.status_code == 413
    assert chamadas == [], 'o tamanho é lido do cabeçalho: nada de alocar a imagem inteira'


def test_o_limite_de_pixels_e_exato_16_milhoes_passam_e_um_pixel_a_mais_nao():
    face._conferir_tamanho_da_imagem(_png_com_dimensoes(4000, 4000))        # 16.000.000: passa

    with pytest.raises(HTTPException) as erro:
        face._conferir_tamanho_da_imagem(_png_com_dimensoes(4001, 4000))    # 16.004.000

    assert erro.value.status_code == 413


def test_foto_de_webcam_comum_continua_aceita():
    imagem = face._decodificar_imagem(_png_base64(640, 480))

    assert imagem.shape == (480, 640, 3)
