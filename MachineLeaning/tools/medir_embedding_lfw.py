"""Mede o reconhecimento por embedding (SFace, S33) no LFW, no protocolo oficial de 6000 pares em 10 dobras.

    py -I tools/medir_embedding_lfw.py <pasta-do-lfw> <pasta-de-saida>

Para cada par calcula o cosseno entre os vetores. Informa: acurácia pelo protocolo (limiar escolhido nas outras 9 dobras), ponto de
equilíbrio (EER), e os limiares que dão FAR de 1 %, 0,1 % e 0,01 % com a FRR correspondente. Só números vão para a pasta de saída
(fora do repositório). As imagens do LFW não são copiadas.
"""
import csv
import json
import os
import sys
import time

import cv2
import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
os.environ.setdefault('ML_ENV', 'development')

from FaceAuth.embedding import ReconhecedorFacial, similaridade_cosseno   # noqa: E402
from FaceAuth.face_recognition import extrair_landmarks_de_imagem   # noqa: E402
from shared import settings   # noqa: E402


def taxas(cossenos, iguais, limiar):
    aceitos = cossenos >= limiar
    frr = float(1 - aceitos[iguais].mean())      # pessoa certa recusada
    far = float(aceitos[~iguais].mean())         # pessoa errada aceita
    return far, frr


def main():
    lfw, saida = sys.argv[1], sys.argv[2]
    os.makedirs(saida, exist_ok=True)
    with open(os.path.join(lfw, 'pairs_hf.csv'), encoding='utf-8') as f:
        pares = list(csv.DictReader(f))

    reconhecedor = ReconhecedorFacial(settings.FACE_MODEL_DIR)
    reconhecedor.carregar()

    cache, t0 = {}, time.time()
    caminhos = sorted({p[c] for p in pares for c in ('image_a_path', 'image_b_path')})
    for i, rel in enumerate(caminhos, 1):
        imagem = cv2.imread(os.path.join(lfw, 'train', rel.replace('/', os.sep)), cv2.IMREAD_COLOR)
        landmarks = extrair_landmarks_de_imagem(imagem) if imagem is not None else None
        cache[rel] = reconhecedor.vetor(imagem, landmarks) if landmarks else None
        if i % 1000 == 0:
            print(f'{i}/{len(caminhos)} imagens, {time.time() - t0:.0f}s', flush=True)

    cossenos, iguais, dobras, sem_rosto = [], [], [], 0
    for p in pares:
        a, b = cache.get(p['image_a_path']), cache.get(p['image_b_path'])
        if a is None or b is None:
            sem_rosto += 1
            continue
        cossenos.append(similaridade_cosseno(a, b))
        iguais.append(p['is_same'] == '1')
        dobras.append(int(p['fold_id']))
    cossenos, iguais, dobras = np.array(cossenos), np.array(iguais), np.array(dobras)

    candidatos = np.round(np.arange(-0.2, 1.0, 0.005), 3)

    # protocolo oficial: escolhe o limiar nas 9 dobras de treino e mede na dobra de teste
    acuracias = []
    for d in sorted(set(dobras)):
        treino, teste = dobras != d, dobras == d
        melhor = max(candidatos, key=lambda t: ((cossenos[treino] >= t) == iguais[treino]).mean())
        acuracias.append(float(((cossenos[teste] >= melhor) == iguais[teste]).mean()))

    curva = []
    for t in candidatos:
        far, frr = taxas(cossenos, iguais, t)
        curva.append({'limiar': float(t), 'FAR': round(far, 5), 'FRR': round(frr, 5)})
    eer = min(curva, key=lambda c: abs(c['FAR'] - c['FRR']))

    def limiar_para_far(alvo):
        ok = [c for c in curva if c['FAR'] <= alvo]
        return min(ok, key=lambda c: c['limiar']) if ok else None

    resultado = {
        'metodo': 'SFace (OpenCV Zoo) + alinhamento por 5 pontos do MediaPipe, cosseno',
        'pares_medidos': int(len(cossenos)), 'pares_sem_rosto': sem_rosto,
        'acuracia_protocolo_10_dobras': {'media': round(float(np.mean(acuracias)), 4), 'desvio': round(float(np.std(acuracias)), 4)},
        'EER': eer,
        'limiar_para_FAR_1%': limiar_para_far(0.01),
        'limiar_para_FAR_0.1%': limiar_para_far(0.001),
        'limiar_para_FAR_0.01%': limiar_para_far(0.0001),
        'limiar_recomendado_pelo_OpenCV_0.363': dict(zip(('FAR', 'FRR'), (round(v, 5) for v in taxas(cossenos, iguais, 0.363)))),
        'cosseno_medio_iguais': round(float(cossenos[iguais].mean()), 4),
        'cosseno_medio_diferentes': round(float(cossenos[~iguais].mean()), 4),
        'comparador_anterior_geometria_landmarks': {'FRR': 0.9417, 'FAR': 0.0183, 'EER_aprox': 0.43},
        'curva': curva,
    }
    with open(os.path.join(saida, 'lfw_embedding.json'), 'w', encoding='utf-8') as f:
        json.dump(resultado, f, ensure_ascii=False, indent=2)
    print(json.dumps({k: v for k, v in resultado.items() if k != 'curva'}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
