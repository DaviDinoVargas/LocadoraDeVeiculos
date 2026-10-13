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
