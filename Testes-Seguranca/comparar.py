"""Compara a última execução com a anterior (ou com duas pastas informadas): quem passou a falhar, quem passou a passar e a variação de latência."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib import ambiente  # noqa: E402


def carregar(pasta: Path):
    return json.loads((pasta / 'resultado.json').read_text(encoding='utf-8'))


def main():
    base = ambiente.pasta_de_resultados()
    if len(sys.argv) == 3:
        antiga, nova = Path(sys.argv[1]), Path(sys.argv[2])
    else:
        execs = sorted(p for p in base.iterdir() if p.is_dir() and (p / 'resultado.json').exists())
        if len(execs) < 2:
            print('É preciso ter pelo menos duas execuções em', base)
            return 1
        antiga, nova = execs[-2], execs[-1]
    a, n = carregar(antiga), carregar(nova)
    print(f'Antes : {antiga.name}\nDepois: {nova.name}\n')
    va = {v['id']: v for v in a['verificacoes'] if not v['informativo']}
    vn = {v['id']: v for v in n['verificacoes'] if not v['informativo']}
    piorou = [i for i in vn if i in va and va[i]['passou'] and not vn[i]['passou']]
    melhorou = [i for i in vn if i in va and not va[i]['passou'] and vn[i]['passou']]
    novos = [i for i in vn if i not in va]
    print(f'Passaram a FALHAR ({len(piorou)}): {", ".join(piorou) or "nenhuma"}')
    print(f'Passaram a PASSAR ({len(melhorou)}): {", ".join(melhorou) or "nenhuma"}')
    print(f'Verificações novas ({len(novos)}): {", ".join(novos) or "nenhuma"}')
    print(f'Aprovadas: {sum(v["passou"] for v in va.values())} -> {sum(v["passou"] for v in vn.values())}'
          f' | Reprovadas: {sum(not v["passou"] for v in va.values())} -> {sum(not v["passou"] for v in vn.values())}')
    ca = {(m['cenario'], m['concorrencia']): m for m in (a.get('carga') or {}).get('medicoes', [])}
    cn = {(m['cenario'], m['concorrencia']): m for m in (n.get('carga') or {}).get('medicoes', [])}
    if ca and cn:
        print('\nCarga (p95 ms antes -> depois):')
        for chave, m in cn.items():
            if chave in ca:
                antes = ca[chave]['p95_ms']
                delta = (m['p95_ms'] - antes) / antes * 100 if antes else 0
                print(f'  {chave[0]:42} conc={chave[1]:<3} {antes:>8} -> {m["p95_ms"]:>8}  ({delta:+.0f}%)')
    return 1 if piorou else 0


if __name__ == '__main__':
    sys.exit(main())
