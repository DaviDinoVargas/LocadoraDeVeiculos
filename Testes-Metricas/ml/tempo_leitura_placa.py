"""Tempo de leitura de placa (OCR) do serviço de ML, sobre placas SINTÉTICAS desenhadas na hora.

O que mede: o tempo do `ocr_plate_image` (até 4 tentativas do Tesseract com pré-processamento) sobre um recorte de placa. O que NÃO mede: a detecção do veículo/placa pelo YOLO nem câmera real (iluminação, ângulo, movimento); a leitura
com a placa real (BRA2E19) continua pendente. Também NÃO mede o acerto: a fonte do OpenCV não é a fonte de placa, então a taxa de
leitura aqui não diz nada sobre placas reais e não é reportada. O tempo serve como custo do OCR e para comparar versões.
Uso:  python tempo_leitura_placa.py [saida_dir] [quantidade]
"""
import json
import random
import statistics
import string
import sys
import time
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(RAIZ / 'MachineLeaning'))

import cv2  # noqa: E402
import numpy as np  # noqa: E402

from yolo_tesseract import tesseract_service as ocr  # noqa: E402


def placa_aleatoria(mercosul: bool) -> str:
    letras = ''.join(random.choices(string.ascii_uppercase, k=3))
    if mercosul:
        return f'{letras}{random.randint(0, 9)}{random.choice(string.ascii_uppercase)}{random.randint(10, 99)}'
    return f'{letras}{random.randint(1000, 9999)}'


def desenhar(placa: str, ruido: float) -> np.ndarray:
    """Recorte de placa: fundo claro, texto escuro, leve desfoque e ruído (a câmera real é pior; isto é o caso fácil)."""
    img = np.full((90, 300, 3), 245, np.uint8)
    cv2.rectangle(img, (2, 2), (297, 87), (30, 30, 30), 3)
    cv2.putText(img, f'{placa[:3]}-{placa[3:]}', (22, 62), cv2.FONT_HERSHEY_DUPLEX, 1.7, (20, 20, 20), 3, cv2.LINE_AA)
    img = cv2.GaussianBlur(img, (3, 3), 0)
    if ruido:
        img = np.clip(img.astype(np.int16) + np.random.normal(0, ruido, img.shape), 0, 255).astype(np.uint8)
    return img


def percentil(valores, p):
    ordenados = sorted(valores)
    return ordenados[min(len(ordenados) - 1, int(round(p / 100 * (len(ordenados) - 1))))]


def main() -> int:
    saida = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('.')
    quantidade = int(sys.argv[2]) if len(sys.argv) > 2 else 40
    disponivel, versao = ocr.ocr_disponivel()
    if not disponivel:
        print(f'Tesseract indisponível ({versao}); medição ignorada.')
        return 1

    random.seed(7)
    np.random.seed(7)
    ocr.ocr_plate_image(desenhar('ABC1D23', 0))   # aquece (carrega o Tesseract) e não entra na estatística

    tempos, devolveu = [], 0
    for i in range(quantidade):
        placa = placa_aleatoria(mercosul=i % 2 == 0)
        img = desenhar(placa, ruido=[0, 6, 12][i % 3])
        inicio = time.perf_counter()
        lida = ocr.ocr_plate_image(img)
        tempos.append((time.perf_counter() - inicio) * 1000)
        devolveu += lida is not None

    resultado = {
        'quantidade': quantidade, 'tesseract': versao,
        'p50_ms': round(statistics.median(tempos), 1), 'p95_ms': round(percentil(tempos, 95), 1), 'max_ms': round(max(tempos), 1),
        'devolveu_placa_pct': round(100 * devolveu / quantidade, 1),
    }
    saida.mkdir(parents=True, exist_ok=True)
    (saida / 'leitura_placa.json').write_text(json.dumps(resultado, ensure_ascii=False, indent=2), encoding='utf-8')
    (saida / 'leitura_placa.md').write_text('\n'.join([
        '# Leitura de placa (OCR) — placas sintéticas', '',
        f'- {quantidade} placas (metade formato antigo, metade Mercosul; ruído 0, 6 e 12), Tesseract {versao}, uma thread.',
        '- **Não inclui** a detecção pelo YOLO nem câmera real; é o custo do OCR sobre um recorte já pronto.', '',
        '- O acerto **não é reportado**: placa desenhada com a fonte do OpenCV não representa placa real. Quando o OCR não acha placa ele roda as',
        '  4 tentativas, então o p95 é o custo do pior caso (leitura limpa na 1ª tentativa é bem mais rápida).', '',
        '| p50 ms | p95 ms | máx ms |', '|---|---|---|',
        f"| {resultado['p50_ms']} | {resultado['p95_ms']} | {resultado['max_ms']} |", ''
    ]), encoding='utf-8')
    print(f"OCR de placa: p50={resultado['p50_ms']} ms p95={resultado['p95_ms']} ms ({quantidade} placas sintéticas)")
    return 0


if __name__ == '__main__':
    sys.exit(main())
