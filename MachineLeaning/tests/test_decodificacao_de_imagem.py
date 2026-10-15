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
