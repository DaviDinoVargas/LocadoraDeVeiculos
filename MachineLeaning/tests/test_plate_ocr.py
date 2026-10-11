"""Valida o formato de placa brasileira aplicado ao texto do OCR
(yolo_tesseract/tesseract_service.py). Não precisa do binário do Tesseract."""
import os
import sys

import pytest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

from yolo_tesseract.tesseract_service import configurar_tesseract, normalizar_placa, ocr_disponivel


@pytest.mark.parametrize('texto,esperado', [
    ('ABC1234', 'ABC1234'),          # padrão antigo
    ('ABC1D23', 'ABC1D23'),          # Mercosul
    ('abc-1d23', 'ABC1D23'),         # minúsculas e separador
    (' ABC 1234\n', 'ABC1234'),      # espaços e quebra de linha do OCR
    ('A8C1234', 'ABC1234'),          # 8 no lugar de B (posição de letra)
    ('ABCI234', 'ABC1234'),          # I no lugar de 1 (posição de dígito)
    ('ABC12Z4', 'ABC1224'),          # Z -> 2 na posição de dígito final
])
def test_normalizar_placa_aceita_e_corrige(texto, esperado):
    assert normalizar_placa(texto) == esperado


@pytest.mark.parametrize('texto', ['', None, 'ABC', 'ABCDEFG', '1234567', 'ABC12345', 'XX1234', '!!!'])
def test_normalizar_placa_rejeita_lixo(texto):
    assert normalizar_placa(texto) is None


def test_ocr_disponivel_nao_levanta_sem_binario():
    # O ambiente de CI/dev pode não ter o Tesseract: a checagem deve devolver
    # (ok, detalhe) em vez de explodir.
    ok, detalhe = ocr_disponivel()
    assert isinstance(ok, bool)
    assert isinstance(detalhe, str) and detalhe


def test_tesseract_cmd_invalido_e_ignorado(monkeypatch):
    import yolo_tesseract.tesseract_service as ts
    monkeypatch.setattr(ts, 'TESSERACT_CMD', r'Z:\nao\existe\tesseract.exe')
    assert configurar_tesseract() != r'Z:\nao\existe\tesseract.exe'
