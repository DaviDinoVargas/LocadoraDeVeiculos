"""Mede, com o LFW (Labeled Faces in the Wild), (1) a taxa de falsa aceitação/rejeição do comparador facial atual
(landmarks do MediaPipe, limiares de FaceAuth/router.py) e (2) quantas fotos de pessoas reais a prova de vida (S3) recusa.

    py -I tools/medir_face_lfw.py <pasta-do-lfw> <pasta-de-saida> [--limite N]

<pasta-do-lfw> tem `pairs_hf.csv` (os 6000 pares oficiais, 10 dobras) e `train/images/...`. Os resultados (JSON) vão para
<pasta-de-saida>, que deve ficar FORA do repositório. Nenhuma imagem é copiada nem guardada: só números.

Limites: o LFW tem fotos da internet de 250x250, não quadros de webcam, e não tem ataques (foto impressa/tela). A taxa de
recusa de pessoas reais aqui é um piso do que a webcam real fará; a capacidade de pegar ataques precisa de teste com a câmera.
"""
import argparse
import csv
import json
import os
import sys
import time

import cv2
import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
os.environ.setdefault('ML_ENV', 'development')

from FaceAuth.antispoof.liveness import ProvaDeVida, caixa_dos_landmarks   # noqa: E402
from FaceAuth.face_recognition import extrair_landmarks_de_imagem, normalizar_landmarks   # noqa: E402
from shared import settings   # noqa: E402

MARGENS = (0.0, 0.1, 0.2)   # folga em volta da caixa dos landmarks, para parecer com a caixa de um detector de rosto


def analisar_imagem(caminho, prova):
    imagem = cv2.imread(caminho, cv2.IMREAD_COLOR)
    if imagem is None:
        return None
    landmarks = extrair_landmarks_de_imagem(imagem)
    if landmarks is None:
        return {'rosto': False}
    altura, largura = imagem.shape[:2]
    vidas = {}
    for m in MARGENS:
        r = prova.avaliar(imagem, caixa_dos_landmarks(landmarks, largura, altura, m))
        vidas[str(m)] = r.pontuacao
    norm = normalizar_landmarks(landmarks)
    return {'rosto': True, 'xy': [[p['x'], p['y']] for p in norm], 'vida': vidas}


def similaridade(a, b, distancia):
    d = np.linalg.norm(np.asarray(a) - np.asarray(b), axis=1)
    return float((d < distancia).mean())


def metricas_do_par(pares, cache, distancia, minima):
    iguais_ok = iguais_n = difer_aceitos = difer_n = 0
    for p in pares:
        a, b = cache.get(p['image_a_path']), cache.get(p['image_b_path'])
        if not a or not b or not a.get('rosto') or not b.get('rosto'):
            continue
        aceito = similaridade(a['xy'], b['xy'], distancia) >= minima
        if p['is_same'] == '1':
            iguais_n += 1
            iguais_ok += aceito
        else:
            difer_n += 1
            difer_aceitos += aceito
    return {
        'pares_iguais': iguais_n, 'pares_diferentes': difer_n,
        'FRR': round(1 - iguais_ok / iguais_n, 4) if iguais_n else None,   # pessoa certa recusada
        'FAR': round(difer_aceitos / difer_n, 4) if difer_n else None,     # pessoa errada aceita
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('lfw')
    ap.add_argument('saida')
    ap.add_argument('--limite', type=int, default=0)
    args = ap.parse_args()
    os.makedirs(args.saida, exist_ok=True)

    with open(os.path.join(args.lfw, 'pairs_hf.csv'), encoding='utf-8') as f:
        pares = list(csv.DictReader(f))
    if args.limite:
        pares = pares[:args.limite // 2] + pares[300:300 + args.limite // 2]   # metade iguais, metade diferentes (1ª dobra)
    caminhos = sorted({p[c] for p in pares for c in ('image_a_path', 'image_b_path')})

    prova = ProvaDeVida(settings.ANTISPOOF_DIR, 0.5)
    prova.carregar()

    cache, t0 = {}, time.time()
    for i, rel in enumerate(caminhos, 1):
        cache[rel] = analisar_imagem(os.path.join(args.lfw, 'train', rel.replace('/', os.sep)), prova)
        if i % 500 == 0:
            print(f'{i}/{len(caminhos)} imagens, {time.time() - t0:.0f}s', flush=True)

    com_rosto = [v for v in cache.values() if v and v.get('rosto')]
    resultado = {
        'imagens': len(caminhos), 'com_rosto_detectado': len(com_rosto),
        'sem_rosto_detectado': len(caminhos) - len(com_rosto),
        'pares': len(pares),
    }

    # (2) prova de vida em fotos de pessoas reais
    vida = {}
    for m in MARGENS:
        notas = np.array([v['vida'][str(m)] for v in com_rosto])
        vida[str(m)] = {
            'media': round(float(notas.mean()), 4),
            'recusadas_no_limiar_0.5': round(float((notas < 0.5).mean()), 4),
            'recusadas_no_limiar_0.3': round(float((notas < 0.3).mean()), 4),
            'percentis_5_25_50_75': [round(float(x), 4) for x in np.percentile(notas, [5, 25, 50, 75])],
        }
    resultado['prova_de_vida_em_fotos_reais_por_margem'] = vida

    # (1) identidade: limiar atual e varredura
    atual = metricas_do_par(pares, cache, 0.02, 0.9)
    resultado['identidade_limiar_atual(0.02;0.9)'] = atual
    varredura = []
    for distancia in (0.005, 0.01, 0.015, 0.02, 0.03, 0.04):
        for minima in (0.5, 0.6, 0.7, 0.8, 0.9, 0.95):
            varredura.append({'distancia': distancia, 'similaridade_minima': minima, **metricas_do_par(pares, cache, distancia, minima)})
    resultado['varredura'] = varredura
    candidatos = [v for v in varredura if v['FAR'] is not None and v['FRR'] is not None]
    if candidatos:
        resultado['mais_proximo_do_ponto_EER'] = min(candidatos, key=lambda v: abs(v['FAR'] - v['FRR']))

    with open(os.path.join(args.saida, 'lfw_resultado.json'), 'w', encoding='utf-8') as f:
        json.dump(resultado, f, ensure_ascii=False, indent=2)

    print(json.dumps({k: v for k, v in resultado.items() if k != 'varredura'}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
