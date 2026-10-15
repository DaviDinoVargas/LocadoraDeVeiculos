"""Lista os arquivos do Angular com mais linhas NÃO cobertas, a partir do coverage-summary.json do Karma (reporter json-summary).
Mostra onde está o buraco de verdade (e não só a porcentagem). Uso:  python angular_relatorio.py <coverage-summary.json> [quantidade]
"""
import json
import sys
from pathlib import Path


def main() -> None:
    dados = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
    quantidade = int(sys.argv[2]) if len(sys.argv) > 2 else 30
    linhas = []
    for caminho, v in dados.items():
        if caminho == 'total':
            continue
        l = v['lines']
        nome = caminho.replace(chr(92), '/').split('src/app/')[-1]
        linhas.append((l['total'] - l['covered'], l['pct'], l['total'], nome))
    linhas.sort(reverse=True)
    total = dados['total']['lines']
    print(f"Total de linhas: {total['pct']} % ({total['covered']} de {total['total']})")
    for faltam, pct, total_arq, nome in linhas[:quantidade]:
        print(f'{faltam:4d} faltam de {total_arq:4d} ({pct:5.1f} %)  {nome}')


if __name__ == '__main__':
    main()
