import os
import re
import shutil

import cv2
import pytesseract

from .config import OCR_LANG, TESSERACT_CMD
from .utils import log

# Placa brasileira: padrão antigo (ABC1234) e Mercosul (ABC1D23) -- 3 letras,
# 1 dígito, 1 letra-ou-dígito, 2 dígitos.
PADRAO_PLACA = re.compile(r'^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$')

# O OCR confunde caracteres parecidos. Como a posição de cada caractere na
# placa é conhecida (letra ou dígito), dá para corrigir a confusão por posição.
_DIGITO_PARA_LETRA = {'0': 'O', '1': 'I', '2': 'Z', '5': 'S', '8': 'B', '6': 'G', '4': 'A', '7': 'T'}
_LETRA_PARA_DIGITO = {'O': '0', 'Q': '0', 'D': '0', 'I': '1', 'L': '1', 'Z': '2', 'S': '5', 'B': '8', 'G': '6', 'A': '4', 'T': '7'}

_CAMINHOS_WINDOWS = (
    r'C:\Program Files\Tesseract-OCR\tesseract.exe',
    r'C:\Program Files (x86)\Tesseract-OCR\tesseract.exe',
)


def configurar_tesseract() -> str | None:
    """Aponta o pytesseract para o binário. Ordem: TESSERACT_CMD, PATH, locais
    padrão do instalador do Windows. Devolve o caminho usado (ou None)."""
    candidatos = [TESSERACT_CMD] if TESSERACT_CMD else []
    candidatos.append(shutil.which('tesseract'))
    candidatos.extend(_CAMINHOS_WINDOWS)
    for caminho in candidatos:
        if caminho and os.path.isfile(caminho):
            pytesseract.pytesseract.tesseract_cmd = caminho
            return caminho
    return None


def ocr_disponivel() -> tuple[bool, str]:
    """(True, versão) se o binário do Tesseract responde; senão (False, motivo)."""
    configurar_tesseract()
    try:
        return True, str(pytesseract.get_tesseract_version())
    except pytesseract.TesseractNotFoundError:
        return False, (
            "Binário do Tesseract não encontrado. Instale o Tesseract OCR e coloque-o no PATH "
            "ou defina TESSERACT_CMD com o caminho do tesseract.exe."
        )
    except Exception as e:  # noqa: BLE001 - qualquer falha do binário vira "indisponível"
        return False, f"Tesseract indisponível: {e}"


def normalizar_placa(texto: str) -> str | None:
    """Limpa o texto do OCR e só devolve algo que tenha formato de placa
    brasileira; lixo vira None (antes qualquer sequência alfanumérica era
    aceita como "placa")."""
    limpo = ''.join(c for c in (texto or '').upper() if c.isalnum())
    if len(limpo) != 7:
        return None

    letras = ''.join(_DIGITO_PARA_LETRA.get(c, c) if not c.isalpha() else c for c in limpo[:3])
    d1 = _LETRA_PARA_DIGITO.get(limpo[3], limpo[3])
    quarto = limpo[4]  # letra (Mercosul) ou dígito (antiga): ambos válidos
    d2 = ''.join(_LETRA_PARA_DIGITO.get(c, c) for c in limpo[5:])

    placa = f'{letras}{d1}{quarto}{d2}'
    return placa if PADRAO_PLACA.match(placa) else None


def _variantes(bgr_image):
    """Pré-processamentos a tentar, do mais simples ao mais agressivo."""
    gray = cv2.cvtColor(bgr_image, cv2.COLOR_BGR2GRAY)
    if gray.shape[0] < 60:  # recortes pequenos: o OCR vai muito melhor ampliado
        fator = 60 / max(gray.shape[0], 1)
        gray = cv2.resize(gray, None, fx=fator, fy=fator, interpolation=cv2.INTER_CUBIC)
    suave = cv2.bilateralFilter(gray, 9, 75, 75)
    yield suave
    yield cv2.adaptiveThreshold(suave, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, 11, 2)
    _, otsu = cv2.threshold(suave, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    yield otsu


def ocr_plate_image(bgr_image) -> str | None:
    """Lê a placa de um recorte. Devolve None se nada com formato de placa.
    Propaga TesseractNotFoundError (o chamador decide o que fazer)."""
    config = "--psm 7 -c tessedit_char_whitelist=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    for imagem in _variantes(bgr_image):
        texto = pytesseract.image_to_string(imagem, lang=OCR_LANG, config=config)
        placa = normalizar_placa(texto)
        if placa:
            return placa
    log.debug('OCR sem placa válida no recorte')
    return None
