"""Junta os relatórios de cobertura (cobertura.xml) dos projetos de teste .NET e calcula a cobertura por LINHA de código de produção.

Mescla por (arquivo, linha) com o MAIOR número de execuções entre os projetos: uma linha conta como coberta se qualquer suíte a executou.
Fica de fora o que não é código do produto: migrations, projetos de teste e código gerado. Uso:
    python dotnet_relatorio.py <pasta-com-os-xml> [saida.md]
"""
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

IGNORAR_ARQUIVO = re.compile(r'(\\|/)(Migrations|obj|bin)(\\|/)|\.g\.cs$|\.designer\.cs$|ModelSnapshot\.cs$', re.I)
IGNORAR_PACOTE = re.compile(r'Teste|Tests', re.I)


def ler(pasta: Path):
    linhas = defaultdict(int)             # (pacote, arquivo, linha) -> execuções
    for xml in pasta.rglob('*.cobertura.xml'):
        for pacote in ET.parse(xml).getroot().iter('package'):
            nome = pacote.get('name', '')
            if IGNORAR_PACOTE.search(nome) or not nome.startswith('LocadoraDeVeiculos'):
                continue
            for classe in pacote.iter('class'):
                arquivo = classe.get('filename', '')
                if IGNORAR_ARQUIVO.search(arquivo):
                    continue
                for linha in classe.iter('line'):
                    chave = (nome, arquivo, int(linha.get('number')))
                    linhas[chave] = max(linhas[chave], int(linha.get('hits', 0)))
    return linhas


def relatorio(linhas) -> str:
    por_pacote, por_arquivo = defaultdict(lambda: [0, 0]), defaultdict(lambda: [0, 0])
    for (pacote, arquivo, _), hits in linhas.items():
        for alvo, chave in ((por_pacote, pacote), (por_arquivo, (pacote, arquivo))):
            alvo[chave][0] += 1
            alvo[chave][1] += 1 if hits > 0 else 0
    total = sum(v[0] for v in por_pacote.values())
    cobertas = sum(v[1] for v in por_pacote.values())
    saida = ['# Cobertura .NET (linhas, suítes mescladas)', '',
             f'**Total: {100 * cobertas / total:.1f} %** ({cobertas} de {total} linhas de produção)', '',
             '| Assembly | Linhas | Cobertas | % |', '|---|---|---|---|']
    for pacote, (n, c) in sorted(por_pacote.items(), key=lambda x: x[1][1] / x[1][0]):
        saida.append(f'| {pacote} | {n} | {c} | {100 * c / n:.1f} |')
    saida += ['', '## Arquivos com mais linhas NÃO cobertas (onde está o buraco)', '', '| Arquivo | Linhas | Não cobertas | % |', '|---|---|---|---|']
    piores = sorted(por_arquivo.items(), key=lambda x: x[1][0] - x[1][1], reverse=True)[:40]
    for (pacote, arquivo), (n, c) in piores:
        saida.append(f"| {Path(arquivo).name} ({pacote.replace('LocadoraDeVeiculos.', '')}) | {n} | {n - c} | {100 * c / n:.0f} |")
    return '\n'.join(saida) + '\n'


if __name__ == '__main__':
    texto = relatorio(ler(Path(sys.argv[1])))
    if len(sys.argv) > 2:
        Path(sys.argv[2]).write_text(texto, encoding='utf-8')
    print(texto.encode('ascii', 'replace').decode()[:3500])
