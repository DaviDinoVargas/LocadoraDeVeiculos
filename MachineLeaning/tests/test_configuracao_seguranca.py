"""S2 / S12: segredo interno sem valor padrão fora de desenvolvimento e /docs só em desenvolvimento."""
import importlib
import os
import sys

import pytest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from shared import settings

TOKEN_FORTE = 'x' * 40


def test_em_desenvolvimento_aceita_o_valor_padrao():
    assert settings.resolver_token_interno({}) == settings.TOKEN_INTERNO_DE_DESENVOLVIMENTO
    assert settings.em_desenvolvimento({}) is True


def test_em_desenvolvimento_usa_o_valor_informado():
    assert settings.resolver_token_interno({'ML_INTERNAL_TOKEN': 'abc'}) == 'abc'


@pytest.mark.parametrize('env', [
    {'ML_ENV': 'production'},
    {'ML_ENV': 'production', 'ML_INTERNAL_TOKEN': ''},
    {'ML_ENV': 'production', 'ML_INTERNAL_TOKEN': settings.TOKEN_INTERNO_DE_DESENVOLVIMENTO},
    {'ML_ENV': 'staging', 'ML_INTERNAL_TOKEN': 'curto'},
])
def test_fora_de_desenvolvimento_recusa_token_ausente_padrao_ou_curto(env):
    with pytest.raises(RuntimeError):
        settings.resolver_token_interno(env)


def test_fora_de_desenvolvimento_aceita_token_longo():
    env = {'ML_ENV': 'production', 'ML_INTERNAL_TOKEN': TOKEN_FORTE}
    assert settings.resolver_token_interno(env) == TOKEN_FORTE
    assert settings.em_desenvolvimento(env) is False


def test_docs_somem_fora_de_desenvolvimento(monkeypatch):
    monkeypatch.setenv('ML_ENV', 'production')
    monkeypatch.setenv('ML_INTERNAL_TOKEN', TOKEN_FORTE)
    importlib.reload(settings)
    try:
        assert settings.DOCS_HABILITADOS is False
    finally:
        monkeypatch.undo()
        importlib.reload(settings)


# ---------- S23: integridade do peso do modelo e retenção dos recortes ----------

def test_peso_com_hash_correto_passa_e_com_hash_errado_falha(tmp_path):
    from shared import integridade
    peso = tmp_path / 'm.pt'
    peso.write_bytes(b'pesos')
    certo = integridade.sha256_do_arquivo(str(peso))

    integridade.verificar_peso(str(peso), certo, em_desenvolvimento=False)
    integridade.verificar_peso(str(peso), certo.upper(), em_desenvolvimento=False)
    with pytest.raises(RuntimeError, match='não confere'):
        integridade.verificar_peso(str(peso), '0' * 64, em_desenvolvimento=False)


def test_peso_sem_hash_so_e_aceito_em_desenvolvimento(tmp_path):
    from shared import integridade
    peso = tmp_path / 'm.pt'
    peso.write_bytes(b'pesos')

    integridade.verificar_peso(str(peso), None, em_desenvolvimento=True)
    with pytest.raises(RuntimeError, match='YOLO_MODEL_SHA256'):
        integridade.verificar_peso(str(peso), None, em_desenvolvimento=False)


def test_limpeza_apaga_so_o_que_passou_do_prazo(tmp_path):
    import time
    from shared import integridade
    antigo, novo = tmp_path / 'antigo.jpg', tmp_path / 'novo.jpg'
    antigo.write_bytes(b'x')
    novo.write_bytes(b'x')
    agora = time.time()
    os.utime(antigo, (agora - 48 * 3600, agora - 48 * 3600))

    assert integridade.limpar_arquivos_antigos(str(tmp_path), horas=24, agora=agora) == 1
    assert not antigo.exists() and novo.exists()
    assert integridade.limpar_arquivos_antigos(str(tmp_path), horas=0) == 0


def test_save_crop_dispara_a_limpeza_de_antigos(tmp_path):
    import time
    import numpy as np
    from shared import integridade
    from yolo_tesseract import plate_localizer
    integridade._ULTIMA_LIMPEZA.clear()
    velho = tmp_path / 'velho.jpg'
    velho.write_bytes(b'x')
    agora = time.time()
    os.utime(velho, (agora - 72 * 3600, agora - 72 * 3600))

    plate_localizer.save_crop(np.zeros((4, 4, 3), dtype=np.uint8), str(tmp_path), prefix='t')

    assert not velho.exists()
    assert any(p.name.startswith('t_') for p in tmp_path.iterdir())


def test_corpos_enormes_sao_recusados_no_facial_e_no_chat():
    from fastapi import FastAPI
    from fastapi.testclient import TestClient
    from FaceAuth import router as face_router
    from ChatBot.router import router as chat_router
    app = FastAPI()
    app.include_router(face_router.router)
    app.include_router(chat_router)
    c = TestClient(app)
    h = {'X-Internal-Token': settings.INTERNAL_TOKEN}

    grande = 'A' * (face_router.MAXIMO_BASE64_CHARS + 1)
    assert c.post('/face/landmarks', json={'imageBase64': grande}, headers=h).status_code == 422
    assert c.post('/face/compare', json={'imageBase64': grande, 'samples': [[0.0]]}, headers=h).status_code == 422
    assert c.post('/chat', json={'message': 'x' * 2001}).status_code == 422
