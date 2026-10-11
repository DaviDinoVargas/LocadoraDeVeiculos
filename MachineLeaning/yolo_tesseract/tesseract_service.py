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

# Quantos caracteres, no máximo, a correção por posição pode trocar para a leitura valer.
MAX_CORRECOES = 1

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


def _correcoes(bruto: str, corrigido: str) -> int:
    return sum(1 for x, y in zip(bruto, corrigido) if x != y)


def extrair_placa(texto: str) -> tuple[str, int] | None:
    """Procura uma placa dentro do texto do OCR, que costuma trazer sobras (a borda da
    placa vira "I"/"T"/"P", o "BRASIL" do Mercosul vira letras soltas). Testa cada janela
    de 7 caracteres e devolve (placa, nº de correções por posição) da que precisou de menos
    correções; empate fica com a primeira."""
    limpo = ''.join(c for c in (texto or '').upper() if c.isalnum())
    melhor = None
    for i in range(0, len(limpo) - 6):
        janela = limpo[i:i + 7]
        placa = normalizar_placa(janela)
        if placa is None:
            continue
        n = _correcoes(janela, placa)
        if n > MAX_CORRECOES:   # corrigiu demais: provavelmente é lixo (ex.: o "BRASIL" lido como placa)
            continue
        if melhor is None or n < melhor[1]:
            melhor = (placa, n)
    return melhor


def _preparar_recorte(bgr_image):
    """Tira a borda preta da placa (vira caractere fantasma no OCR) e a faixa azul do
    "BRASIL" das placas Mercosul."""
    h, w = bgr_image.shape[:2]
    if h < 8 or w < 8:
        return bgr_image
    mx, my = max(1, int(w * 0.035)), max(1, int(h * 0.06))
    interno = bgr_image[my:h - my, mx:w - mx]
    ih = interno.shape[0]
    faixa = interno[:max(1, int(ih * 0.24))]
    hsv = cv2.cvtColor(faixa, cv2.COLOR_BGR2HSV)
    azul = ((hsv[..., 0] > 95) & (hsv[..., 0] < 135) & (hsv[..., 1] > 80)).mean()
    if azul > 0.35:   # faixa azul presente: descarta o topo
        interno = interno[int(ih * 0.26):]
    return interno if interno.size else bgr_image


def _variantes(bgr_image):
    """Pré-processamentos a tentar, do mais simples ao mais agressivo."""
    gray = cv2.cvtColor(bgr_image, cv2.COLOR_BGR2GRAY)
    if gray.shape[0] < 80:  # recortes pequenos: o OCR vai muito melhor ampliado
        fator = 80 / max(gray.shape[0], 1)
        gray = cv2.resize(gray, None, fx=fator, fy=fator, interpolation=cv2.INTER_CUBIC)
    suave = cv2.bilateralFilter(gray, 9, 75, 75)
    yield suave
    _, otsu = cv2.threshold(suave, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    yield otsu
    yield cv2.adaptiveThreshold(suave, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, 21, 8)


def _com_margem(imagem):
    # o Tesseract reconhece melhor texto com folga em volta
    return cv2.copyMakeBorder(imagem, 12, 12, 12, 12, cv2.BORDER_CONSTANT, value=255)


def ocr_plate_image(bgr_image) -> str | None:
    """Lê a placa de um recorte. Devolve None se nada com formato de placa.
    Propaga TesseractNotFoundError (o chamador decide o que fazer)."""
    whitelist = "-c tessedit_char_whitelist=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
    variantes = list(_variantes(_preparar_recorte(bgr_image)))
    tentativas = [(variantes[0], 7), (variantes[0], 8), (variantes[1], 7), (variantes[2], 7)]

    leituras = []
    for imagem, psm in tentativas:
        texto = pytesseract.image_to_string(_com_margem(imagem), lang=OCR_LANG, config=f"--psm {psm} {whitelist}")
        achada = extrair_placa(texto)
        log.debug('OCR psm %s: %r -> %s', psm, texto.strip(), achada)
        if achada is None:
            continue
        if achada[1] == 0:       # leitura limpa, sem correção por posição: confia
            return achada[0]
        leituras.append(achada[0])

    if not leituras:
        return None
    # sem leitura limpa: vale a placa que mais se repetiu entre as tentativas
    return max(set(leituras), key=leituras.count)


# Resolve o binário já no import: quem chamar ocr_plate_image direto (testes, scripts) não
# depende de ter passado antes por ocr_disponivel().
configurar_tesseract()
