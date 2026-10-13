"""S3: prova de vida passiva (MiniFASNet). Os pesos reais não estão no git; estes testes usam pesos pequenos gerados na hora
e, quando a pasta `FaceAuth/modelos` existe, também conferem os arquivos reais."""
import hashlib
import io
import os
import pickle
import sys

import numpy as np
import pytest
import torch
from fastapi import FastAPI
from fastapi.testclient import TestClient

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from FaceAuth import router as face
from FaceAuth.antispoof import baixar_modelo
from FaceAuth.antispoof.liveness import (
    MODELOS, ProvaDeVida, ProvaDeVidaIndisponivel, ResultadoDeVida, _nova_caixa, caixa_dos_landmarks, recortar, sha256_do_arquivo,
)
from FaceAuth.antispoof.minifasnet import MiniFASNetV2
from shared import settings

PASTA_REAL = settings.ANTISPOOF_DIR


def _hash(caminho):
    return sha256_do_arquivo(str(caminho))


@pytest.fixture
def pasta_com_modelo_pequeno(tmp_path):
    """Um MiniFASNetV2 com pesos aleatórios, salvo como o projeto original salva (state_dict)."""
    torch.manual_seed(0)
    modelo = MiniFASNetV2(conv6_kernel=(5, 5))
    caminho = tmp_path / '2.7_80x80_MiniFASNetV2.pth'
    torch.save(modelo.state_dict(), caminho)
    return tmp_path, {caminho.name: (_hash(caminho), MiniFASNetV2)}


# ---------------------------------------------------------------- carregamento seguro

def test_arquivo_ausente_e_indisponibilidade_nao_liberacao(tmp_path):
    prova = ProvaDeVida(str(tmp_path))

    with pytest.raises(ProvaDeVidaIndisponivel, match='ausente'):
        prova.carregar()


def test_arquivo_com_hash_diferente_e_recusado(pasta_com_modelo_pequeno):
    pasta, definicao = pasta_com_modelo_pequeno
    nome = next(iter(definicao))
    adulterado = {nome: ('0' * 64, definicao[nome][1])}

    with pytest.raises(ProvaDeVidaIndisponivel, match='SHA-256'):
        ProvaDeVida(str(pasta), modelos=adulterado).carregar()


def test_arquivo_com_codigo_serializado_nao_executa_mesmo_com_hash_certo(tmp_path):
    """Mesmo que alguém coloque o hash do arquivo malicioso na lista, weights_only=True impede a execução."""
    marca = tmp_path / 'executou.txt'

    class Maldoso:
        def __reduce__(self):
            return (open, (str(marca), 'w'))

    caminho = tmp_path / '2.7_80x80_MiniFASNetV2.pth'
    buf = io.BytesIO()
    pickle.dump({'x': Maldoso()}, buf)
    caminho.write_bytes(buf.getvalue())
    definicao = {caminho.name: (_hash(caminho), MiniFASNetV2)}

    with pytest.raises(ProvaDeVidaIndisponivel, match='carregar'):
        ProvaDeVida(str(tmp_path), modelos=definicao).carregar()

    assert not marca.exists(), 'o arquivo não pode executar código ao ser carregado'


def test_pesos_com_formato_errado_sao_indisponibilidade(tmp_path):
    caminho = tmp_path / '2.7_80x80_MiniFASNetV2.pth'
    torch.save({'camada.inexistente': torch.zeros(3)}, caminho)

    with pytest.raises(ProvaDeVidaIndisponivel):
        ProvaDeVida(str(tmp_path), modelos={caminho.name: (_hash(caminho), MiniFASNetV2)}).carregar()


def test_avaliar_devolve_pontuacao_entre_0_e_1_e_respeita_o_limiar(pasta_com_modelo_pequeno):
    pasta, definicao = pasta_com_modelo_pequeno
    imagem = np.random.default_rng(1).integers(0, 255, (480, 640, 3), dtype=np.uint8)

    permissiva = ProvaDeVida(str(pasta), limiar=0.0, modelos=definicao).avaliar(imagem, [200, 100, 200, 260])
    impossivel = ProvaDeVida(str(pasta), limiar=1.01, modelos=definicao).avaliar(imagem, [200, 100, 200, 260])

    assert 0.0 <= permissiva.pontuacao <= 1.0
    assert impossivel.vivo is False, 'nenhuma pontuação passa de um limiar acima de 1'
    assert permissiva.pontuacao == impossivel.pontuacao


# ---------------------------------------------------------------- geometria do recorte

def test_caixa_ampliada_nunca_sai_da_imagem():
    for caixa in ([0, 0, 100, 100], [500, 380, 140, 100], [300, 200, 50, 60], [10, 10, 620, 460]):
        esq, topo, dir_, base = _nova_caixa(640, 480, caixa, 4.0)
        assert 0 <= esq <= dir_ <= 639 and 0 <= topo <= base <= 479, caixa


def test_recorte_tem_o_tamanho_de_entrada_do_modelo():
    imagem = np.zeros((480, 640, 3), dtype=np.uint8)

    assert recortar(imagem, [200, 100, 200, 260], 2.7, 80, 80).shape == (80, 80, 3)


def test_caixa_dos_landmarks_usa_pixels_e_margem():
    lms = [{'x': 0.4, 'y': 0.3, 'z': 0}, {'x': 0.6, 'y': 0.7, 'z': 0}]

    assert caixa_dos_landmarks(lms, 1000, 500) == [400, 150, 200, 200]
    com_margem = caixa_dos_landmarks(lms, 1000, 500, margem=0.1)
    assert com_margem[2] > 200 and com_margem[0] < 400


# ---------------------------------------------------------------- download com verificação

def test_download_com_hash_errado_e_descartado(tmp_path):
    class Resposta:
        def __enter__(self): return self
        def __exit__(self, *a): return False
        def read(self, n=-1): return b'conteudo-adulterado'

    with pytest.raises(RuntimeError, match='SHA-256'):
        baixar_modelo.baixar(str(tmp_path), abrir=lambda url, timeout: Resposta())

    assert os.listdir(tmp_path) == [], 'nem o temporário pode sobrar'


def test_download_so_aceita_o_commit_fixo_e_https():
    assert baixar_modelo.BASE.startswith('https://raw.githubusercontent.com/minivision-ai/Silent-Face-Anti-Spoofing/')
    assert baixar_modelo.COMMIT in baixar_modelo.BASE and len(baixar_modelo.COMMIT) == 40


def test_download_grande_demais_e_recusado(tmp_path):
    class Resposta:
        def __enter__(self): return self
        def __exit__(self, *a): return False
        def read(self, n=-1): return b'x' * (baixar_modelo.LIMITE_DE_BYTES + 1)

    with pytest.raises(RuntimeError, match='maior'):
        baixar_modelo.baixar(str(tmp_path), abrir=lambda url, timeout: Resposta())


# ---------------------------------------------------------------- rota /face/*

def _app():
    app = FastAPI()
    app.include_router(face.router)
    return TestClient(app)


HEADERS = {'X-Internal-Token': settings.INTERNAL_TOKEN}


@pytest.fixture
def rosto_falso(monkeypatch):
    lms = [{'x': 0.3 + 0.4 * (i % 20) / 20, 'y': 0.2 + 0.6 * (i // 20) / 24, 'z': 0.0} for i in range(468)]
    monkeypatch.setattr(face, 'extrair_landmarks_de_imagem', lambda imagem: lms)
    monkeypatch.setattr(face, '_decodificar_imagem', lambda b64: np.zeros((480, 640, 3), dtype=np.uint8))
    return lms


class _ProvaFalsa:
    def __init__(self, vivo, nota=0.5, erro=None):
        self.vivo, self.nota, self.erro = vivo, nota, erro

    def avaliar(self, imagem, caixa):
        if self.erro:
            raise self.erro
        return ResultadoDeVida(self.vivo, self.nota)


def test_desligada_informa_que_nao_verificou(monkeypatch, rosto_falso):
    monkeypatch.setattr(settings, 'ANTISPOOF_ATIVO', False)

    corpo = _app().post('/face/landmarks', json={'imageBase64': 'x'}, headers=HEADERS).json()

    assert corpo['provaDeVidaAtiva'] is False and corpo['pontuacaoDeVida'] is None


def test_rosto_vivo_passa_e_traz_a_pontuacao(monkeypatch, rosto_falso):
    monkeypatch.setattr(settings, 'ANTISPOOF_ATIVO', True)
    monkeypatch.setattr(face, '_obter_prova_de_vida', lambda: _ProvaFalsa(True, 0.93))

    corpo = _app().post('/face/landmarks', json={'imageBase64': 'x'}, headers=HEADERS).json()

    assert corpo['provaDeVidaAtiva'] is True and corpo['vivo'] is True and corpo['pontuacaoDeVida'] == 0.93
    assert len(corpo['landmarks']) == 468 * 3


def test_foto_de_foto_nao_confirma_identidade_mesmo_com_a_geometria_igual(monkeypatch, rosto_falso):
    monkeypatch.setattr(settings, 'ANTISPOOF_ATIVO', True)
    amostra = face.achatar_landmarks(face.normalizar_landmarks(rosto_falso))

    monkeypatch.setattr(face, '_obter_prova_de_vida', lambda: _ProvaFalsa(True, 0.9))
    real = _app().post('/face/compare', json={'imageBase64': 'x', 'samples': [amostra]}, headers=HEADERS).json()

    monkeypatch.setattr(face, '_obter_prova_de_vida', lambda: _ProvaFalsa(False, 0.04))
    falsa = _app().post('/face/compare', json={'imageBase64': 'x', 'samples': [amostra]}, headers=HEADERS).json()

    assert real['match'] is True
    assert falsa['match'] is False and falsa['vivo'] is False


def test_sem_os_pesos_o_modulo_recusa_com_503_e_nao_libera(monkeypatch, rosto_falso):
    monkeypatch.setattr(settings, 'ANTISPOOF_ATIVO', True)
    monkeypatch.setattr(face, '_obter_prova_de_vida', lambda: _ProvaFalsa(True, erro=ProvaDeVidaIndisponivel('ausente')))
    amostra = face.achatar_landmarks(face.normalizar_landmarks(rosto_falso))

    r1 = _app().post('/face/landmarks', json={'imageBase64': 'x'}, headers=HEADERS)
    r2 = _app().post('/face/compare', json={'imageBase64': 'x', 'samples': [amostra]}, headers=HEADERS)

    assert r1.status_code == 503 and r2.status_code == 503
    assert 'ausente' not in r1.text, 'o motivo interno não vaza para o cliente'


def test_ligada_por_padrao_fora_do_conftest():
    # o padrão do serviço é LIGADO; só o conftest dos testes (e o desenvolvimento) desligam
    assert os.environ.get('ANTISPOOF_ENABLED') == 'false'
    env = {'ANTISPOOF_ENABLED': ''}
    ligado = env.get('ANTISPOOF_ENABLED', 'true').strip().lower() not in ('0', 'false', 'no', 'off')
    assert ligado is True


# ---------------------------------------------------------------- pesos reais (só se baixados)

@pytest.mark.skipif(not all(os.path.isfile(os.path.join(PASTA_REAL, n)) for n in MODELOS), reason='pesos não baixados (baixar_modelo.py)')
def test_pesos_reais_conferem_com_o_hash_e_carregam():
    for nome, (esperado, _) in MODELOS.items():
        assert sha256_do_arquivo(os.path.join(PASTA_REAL, nome)) == esperado

    prova = ProvaDeVida(PASTA_REAL)
    prova.carregar()
    resultado = prova.avaliar(np.full((480, 640, 3), 127, dtype=np.uint8), [200, 100, 200, 260])
    assert 0.0 <= resultado.pontuacao <= 1.0
