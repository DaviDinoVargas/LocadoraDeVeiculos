"""S33: reconhecimento facial por embedding (SFace). O modelo real não está no git; os testes de carga usam arquivos de mentira e,
quando a pasta `FaceAuth/modelos` tem o modelo, também conferem o arquivo real."""
import hashlib
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from FaceAuth import baixar_sface
from FaceAuth.embedding import (
    NOME_DO_ARQUIVO, SHA256_ESPERADO, TAMANHO_DO_VETOR, TAMANHO_ESPERADO, ReconhecedorFacial, ReconhecimentoIndisponivel,
    cinco_pontos, linha_do_rosto, similaridade_cosseno,
)
from shared import settings

PASTA_REAL = settings.FACE_MODEL_DIR


def _landmarks_sinteticos(troca_lados=False):
    lms = [{'x': 0.5, 'y': 0.5, 'z': 0.0} for _ in range(468)]
    esquerda, direita = (0.35, 0.65) if not troca_lados else (0.65, 0.35)
    for i in (33, 133):
        lms[i] = {'x': esquerda, 'y': 0.40, 'z': 0.0}
    for i in (362, 263):
        lms[i] = {'x': direita, 'y': 0.40, 'z': 0.0}
    lms[1] = {'x': 0.50, 'y': 0.55, 'z': 0.0}
    # a boca do mesmo rosto: 0.40 e 0.60; com os lados trocados é o MediaPipe que chama cada canto pelo outro nome
    lms[61] = {'x': 0.40 if not troca_lados else 0.60, 'y': 0.70, 'z': 0.0}
    lms[291] = {'x': 0.60 if not troca_lados else 0.40, 'y': 0.70, 'z': 0.0}
    return lms


# ---------------------------------------------------------------- cosseno

def test_cosseno_de_vetores_iguais_ortogonais_e_opostos():
    a = [1.0] + [0.0] * 127
    b = [0.0, 1.0] + [0.0] * 126

    assert similaridade_cosseno(a, a) == pytest.approx(1.0)
    assert similaridade_cosseno(a, b) == pytest.approx(0.0)
    assert similaridade_cosseno(a, [-v for v in a]) == pytest.approx(-1.0)


def test_cosseno_ignora_a_escala_do_vetor():
    a = [1.0, 2.0, 3.0] + [0.0] * 125
    assert similaridade_cosseno(a, [v * 10 for v in a]) == pytest.approx(1.0)


@pytest.mark.parametrize('a,b', [
    ([1.0] * 127, [1.0] * 128), ([1.0] * 128, [1.0] * 1404), ([], []), ([0.0] * 128, [1.0] * 128),
])
def test_vetores_de_tamanho_errado_ou_nulos_nunca_batem(a, b):
    assert similaridade_cosseno(a, b) == -1.0


# ---------------------------------------------------------------- alinhamento

def test_cinco_pontos_saem_ordenados_da_esquerda_para_a_direita_da_imagem():
    normal = cinco_pontos(_landmarks_sinteticos(), 1000, 1000)
    invertido = cinco_pontos(_landmarks_sinteticos(troca_lados=True), 1000, 1000)

    assert normal.shape == (5, 2)
    assert normal[0][0] < normal[1][0] and normal[3][0] < normal[4][0]
    assert np.allclose(normal, invertido), 'a ordem não pode depender de qual lado o MediaPipe chama de direito'
    assert normal[0][0] == pytest.approx(350) and normal[2][1] == pytest.approx(550)


def test_linha_do_rosto_tem_o_formato_do_detector():
    linha = linha_do_rosto(_landmarks_sinteticos(), 640, 480)

    assert linha.shape == (1, 15) and linha.dtype == np.float32
    assert linha[0, 2] > 0 and linha[0, 3] > 0, 'largura e altura da caixa'
    assert linha[0, 14] == 1.0


# ---------------------------------------------------------------- carregamento seguro

def test_modelo_ausente_e_indisponibilidade(tmp_path):
    with pytest.raises(ReconhecimentoIndisponivel, match='ausente'):
        ReconhecedorFacial(str(tmp_path)).carregar()


def test_arquivo_de_tamanho_errado_e_recusado(tmp_path):
    (tmp_path / NOME_DO_ARQUIVO).write_bytes(b'x' * 100)

    with pytest.raises(ReconhecimentoIndisponivel, match='tamanho'):
        ReconhecedorFacial(str(tmp_path)).carregar()


def test_arquivo_com_hash_diferente_e_recusado_mesmo_com_o_tamanho_certo(tmp_path):
    (tmp_path / 'falso.onnx').write_bytes(b'x' * 100)

    with pytest.raises(ReconhecimentoIndisponivel, match='SHA-256'):
        ReconhecedorFacial(str(tmp_path), nome='falso.onnx', sha256='0' * 64, tamanho=100).carregar()


def test_arquivo_com_hash_certo_mas_que_nao_e_modelo_vira_indisponibilidade(tmp_path):
    conteudo = b'isto nao e um onnx'
    (tmp_path / 'lixo.onnx').write_bytes(conteudo)

    with pytest.raises(ReconhecimentoIndisponivel):
        ReconhecedorFacial(str(tmp_path), nome='lixo.onnx', sha256=hashlib.sha256(conteudo).hexdigest(), tamanho=len(conteudo)).carregar()


# ---------------------------------------------------------------- download com verificação

def test_download_com_hash_errado_e_descartado(tmp_path):
    class Resposta:
        def __enter__(self): return self
        def __exit__(self, *a): return False
        def read(self, n=-1): return b'x' * TAMANHO_ESPERADO

    with pytest.raises(RuntimeError, match='SHA-256'):
        baixar_sface.baixar(str(tmp_path), abrir=lambda url, timeout: Resposta())

    assert os.listdir(tmp_path) == [], 'nem o temporário pode sobrar'


def test_download_de_tamanho_diferente_e_descartado(tmp_path):
    class Resposta:
        def __enter__(self): return self
        def __exit__(self, *a): return False
        def read(self, n=-1): return b'x' * 10

    with pytest.raises(RuntimeError, match='tamanho'):
        baixar_sface.baixar(str(tmp_path), abrir=lambda url, timeout: Resposta())


def test_download_so_de_commit_fixo_por_https():
    assert baixar_sface.URL.startswith('https://media.githubusercontent.com/media/opencv/opencv_zoo/')
    assert baixar_sface.COMMIT in baixar_sface.URL and len(baixar_sface.COMMIT) == 40
    assert len(SHA256_ESPERADO) == 64


# ---------------------------------------------------------------- modelo real (só se baixado)

@pytest.mark.skipif(not os.path.isfile(os.path.join(PASTA_REAL, NOME_DO_ARQUIVO)), reason='SFace não baixado (FaceAuth/baixar_sface.py)')
def test_modelo_real_confere_e_gera_vetor_de_128_numeros_normalizado_e_deterministico():
    reconhecedor = ReconhecedorFacial(PASTA_REAL)
    imagem = np.random.default_rng(3).integers(0, 255, (480, 640, 3), dtype=np.uint8)
    landmarks = _landmarks_sinteticos()

    v1 = reconhecedor.vetor(imagem, landmarks)
    v2 = reconhecedor.vetor(imagem, landmarks)

    assert len(v1) == TAMANHO_DO_VETOR
    assert np.linalg.norm(v1) == pytest.approx(1.0)
    assert similaridade_cosseno(v1, v2) == pytest.approx(1.0)
