"""Tamanho do pacote do front (build de produção): inicial x sob demanda, bruto e comprimido (gzip -6, como o servidor entrega).

"Inicial" = o que o index.html pede de cara (scripts, folhas de estilo e modulepreload). O resto são chunks de rota carregados
sob demanda. É a métrica de tamanho do item D do roteiro de documentação; não depende de navegador."""
import gzip
import re
from pathlib import Path


def _tamanhos(arquivo: Path) -> tuple[int, int]:
    dados = arquivo.read_bytes()
    return len(dados), len(gzip.compress(dados, 6))


def medir(pasta: Path) -> dict:
    index = (pasta / 'index.html').read_text(encoding='utf-8')
    iniciais = {m for m in re.findall(r'(?:src|href)="([^"]+\.(?:js|css))"', index)}
    arquivos = {a.name: a for a in pasta.rglob('*') if a.is_file() and a.suffix in ('.js', '.css')}

    def somar(nomes):
        bruto = comprimido = 0
        for n in nomes:
            b, c = _tamanhos(arquivos[n])
            bruto += b
            comprimido += c
        return {'arquivos': len(nomes), 'bruto_kb': round(bruto / 1024, 1), 'gzip_kb': round(comprimido / 1024, 1)}

    nomes_iniciais = {Path(i).name for i in iniciais if Path(i).name in arquivos}
    # chunks que os iniciais importam ESTATICAMENTE (from "./chunk-X.js" ou import "./chunk-X.js") também são iniciais;
    # import("./chunk-X.js") com parênteses é sob demanda e fica de fora
    fila = list(nomes_iniciais)
    while fila:
        atual = arquivos[fila.pop()]
        if atual.suffix != '.js':
            continue
        for destino in re.findall(r'(?:from\s*|import\s*)"\./([^"]+\.js)"', atual.read_text(encoding='utf-8', errors='ignore')):
            if destino in arquivos and destino not in nomes_iniciais:
                nomes_iniciais.add(destino)
                fila.append(destino)
    sob_demanda = set(arquivos) - nomes_iniciais
    maiores = sorted(((arquivos[n].stat().st_size, n) for n in sob_demanda), reverse=True)[:6]
    return {
        'inicial': somar(nomes_iniciais),
        'sob_demanda': somar(sob_demanda),
        'total': somar(set(arquivos)),
        'maiores_sob_demanda': [{'arquivo': n, 'bruto_kb': round(t / 1024, 1)} for t, n in maiores],
    }


def para_markdown(m: dict) -> str:
    l = ['# Tamanho do pacote do front (produção)', '', '| Parte | Arquivos | Bruto KB | Comprimido (gzip) KB |', '|---|---|---|---|']
    for chave, rotulo in (('inicial', 'Inicial (index.html)'), ('sob_demanda', 'Sob demanda (rotas e bibliotecas)'), ('total', 'Total')):
        p = m[chave]
        l.append(f"| {rotulo} | {p['arquivos']} | {p['bruto_kb']} | {p['gzip_kb']} |")
    l += ['', 'Maiores arquivos sob demanda:', '']
    l += [f"- `{x['arquivo']}` — {x['bruto_kb']} KB" for x in m['maiores_sob_demanda']]
    return '\n'.join(l) + '\n'
