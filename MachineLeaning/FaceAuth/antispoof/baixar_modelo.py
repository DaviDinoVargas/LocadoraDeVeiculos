"""Baixa os pesos da prova de vida (S3) de uma fonte e um commit FIXOS e confere o SHA-256 antes de aceitar.

    py FaceAuth/antispoof/baixar_modelo.py [pasta-de-destino]

Fonte: github.com/minivision-ai/Silent-Face-Anti-Spoofing, commit b6d5f04ad78778917853b25c778acef6d5626d15 (licença Apache-2.0),
pelo domínio raw.githubusercontent.com sobre HTTPS. Cada arquivo é gravado num temporário, o hash é conferido e só então ele vira
o arquivo final: um download adulterado ou truncado nunca fica no lugar. Nada é instalado no sistema e nada é executado.
"""
import os
import sys
import tempfile
import urllib.request

COMMIT = 'b6d5f04ad78778917853b25c778acef6d5626d15'
BASE = f'https://raw.githubusercontent.com/minivision-ai/Silent-Face-Anti-Spoofing/{COMMIT}/resources/anti_spoof_models/'
LIMITE_DE_BYTES = 5 * 1024 * 1024   # cada peso tem ~1,8 MB: acima disso é suspeito

try:
    from .liveness import MODELOS, sha256_do_arquivo
except ImportError:   # executado como script
    sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')))
    from FaceAuth.antispoof.liveness import MODELOS, sha256_do_arquivo


def pasta_padrao() -> str:
    return os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'modelos'))


def baixar(pasta: str, abrir=urllib.request.urlopen) -> list[str]:
    os.makedirs(pasta, exist_ok=True)
    gravados = []

    for nome, (hash_esperado, _) in MODELOS.items():
        destino = os.path.join(pasta, nome)

        if os.path.isfile(destino) and sha256_do_arquivo(destino) == hash_esperado:
            print(f'{nome}: já presente e confere.')
            gravados.append(destino)
            continue

        with abrir(BASE + nome, timeout=60) as resposta:
            conteudo = resposta.read(LIMITE_DE_BYTES + 1)

        if len(conteudo) > LIMITE_DE_BYTES:
            raise RuntimeError(f'{nome}: maior que o esperado; recusado.')

        with tempfile.NamedTemporaryFile(dir=pasta, delete=False, suffix='.parte') as tmp:
            tmp.write(conteudo)
            caminho_tmp = tmp.name

        if sha256_do_arquivo(caminho_tmp) != hash_esperado:
            os.remove(caminho_tmp)
            raise RuntimeError(f'{nome}: o SHA-256 não confere com o esperado; arquivo descartado.')

        os.replace(caminho_tmp, destino)
        print(f'{nome}: baixado e conferido.')
        gravados.append(destino)

    return gravados


if __name__ == '__main__':
    baixar(sys.argv[1] if len(sys.argv) > 1 else pasta_padrao())
