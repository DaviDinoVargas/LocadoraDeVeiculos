"""Resume um relatório do Stryker.NET (mutation-report.json): pontuação por arquivo e a lista dos mutantes que SOBREVIVERAM.

Mutante que sobrevive = uma alteração no código de produção (trocar `==` por `!=`, apagar uma linha, inverter uma condição) que nenhum
teste percebeu. É o jeito de saber se os testes verificam comportamento ou só executam código: cobertura de linhas não mostra isso.
Uso:  python mutacao_resumo.py <mutation-report.json> [--saida resumo.md]
"""
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

MORTOS = {'Killed', 'Timeout', 'CompileError'}
VALIDOS = {'Killed', 'Timeout', 'Survived', 'NoCoverage'}


def resumir(relatorio: dict) -> str:
    por_arquivo = defaultdict(Counter)
    sobreviventes = []
    for caminho, dados in relatorio['files'].items():
        nome = Path(caminho.replace('\\', '/')).name
        linhas = dados['source'].split('\n')
        for m in dados['mutants']:
            if m['status'] in VALIDOS:
                por_arquivo[nome][m['status']] += 1
            if m['status'] in ('Survived', 'NoCoverage'):
                n = m['location']['start']['line']
                sobreviventes.append((nome, n, m['status'], m['mutatorName'], (m.get('replacement') or '')[:50], linhas[n - 1].strip()[:100]))

    total = Counter()
    saida = ['# Mutação (Stryker.NET)', '', '| Arquivo | Mortos | Sobreviveram | Sem cobertura | Pontuação |', '|---|---|---|---|---|']
    for nome, c in sorted(por_arquivo.items()):
        mortos = c['Killed'] + c['Timeout']
        validos = mortos + c['Survived'] + c['NoCoverage']
        total.update(c)
        saida.append(f"| {nome} | {mortos} | {c['Survived']} | {c['NoCoverage']} | {100 * mortos / validos:.0f} % |")
    mortos = total['Killed'] + total['Timeout']
    validos = mortos + total['Survived'] + total['NoCoverage']
    saida.insert(2, f'**Pontuação geral: {100 * mortos / validos:.1f} %** ({mortos} de {validos} mutantes mortos)')
    saida.insert(3, '')
    saida += ['', '## Mutantes que sobreviveram (o que os testes ainda não verificam)', '', '| Arquivo:linha | Situação | Mutação | Código |', '|---|---|---|---|']
    for nome, n, status, mutador, trocado, codigo in sobreviventes:
        saida.append(f"| {nome}:{n} | {'sem teste' if status == 'NoCoverage' else 'sobreviveu'} | {mutador} → `{trocado}` | `{codigo}` |")
    return '\n'.join(saida) + '\n'


if __name__ == '__main__':
    texto = resumir(json.loads(Path(sys.argv[1]).read_text(encoding='utf-8')))
    if '--saida' in sys.argv:
        Path(sys.argv[sys.argv.index('--saida') + 1]).write_text(texto, encoding='utf-8')
    print(texto.encode('ascii', 'replace').decode()[:6000])
