"""Mostra as LINHAS sem cobertura de um arquivo do Angular, a partir do coverage-final.json do Karma (reporter `json`).
Uso:  python angular_linhas.py <coverage-final.json> <trecho-do-caminho>   (ex.: data-table.component.ts)
Imprime o número da linha e o código, para decidir se vale um teste (comportamento) ou se é só ruído (log, ramo defensivo).
"""
import json
import sys
from pathlib import Path


def main() -> None:
    dados = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
    trecho = sys.argv[2].replace('\\', '/')
    for caminho, v in dados.items():
        norm = caminho.replace('\\', '/')
        if trecho not in norm:
            continue
        fonte = Path(caminho).read_text(encoding='utf-8', errors='ignore').splitlines() if Path(caminho).exists() else []
        sem = sorted({v['statementMap'][k]['start']['line'] for k, n in v['s'].items() if n == 0})
        print(f'{norm.split("src/app/")[-1]}: {len(sem)} linhas sem cobertura')
        for linha in sem:
            codigo = fonte[linha - 1].strip() if 0 < linha <= len(fonte) else ''
            print(f'{linha:5d}  {codigo[:140]}')


if __name__ == '__main__':
    main()
