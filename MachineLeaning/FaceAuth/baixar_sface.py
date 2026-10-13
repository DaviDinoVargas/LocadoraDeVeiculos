"""Baixa o modelo de reconhecimento facial SFace (S33) de uma fonte e um commit FIXOS e confere o SHA-256 e o tamanho.

    py FaceAuth/baixar_sface.py [pasta-de-destino]

Fonte: github.com/opencv/opencv_zoo, commit 47534e27c9851bb1128ccc0102f1145e27f23f98 (licença Apache-2.0), arquivo guardado com Git LFS,
servido por media.githubusercontent.com sobre HTTPS. O hash esperado é o `oid sha256` do ponteiro LFS do próprio repositório. O arquivo é
gravado num temporário, conferido, e só então vira o arquivo final. Nada é instalado e nada é executado.
"""
import os
import sys
import tempfile
import urllib.request

COMMIT = '47534e27c9851bb1128ccc0102f1145e27f23f98'
URL = f'https://media.githubusercontent.com/media/opencv/opencv_zoo/{COMMIT}/models/face_recognition_sface/face_recognition_sface_2021dec.onnx'

try:
    from .embedding import NOME_DO_ARQUIVO, SHA256_ESPERADO, TAMANHO_ESPERADO, _sha256
except ImportError:   # executado como script
    sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
    from FaceAuth.embedding import NOME_DO_ARQUIVO, SHA256_ESPERADO, TAMANHO_ESPERADO, _sha256


def pasta_padrao() -> str:
    return os.path.abspath(os.path.join(os.path.dirname(__file__), 'modelos'))


def baixar(pasta: str, abrir=urllib.request.urlopen) -> str:
    os.makedirs(pasta, exist_ok=True)
    destino = os.path.join(pasta, NOME_DO_ARQUIVO)

    if os.path.isfile(destino) and os.path.getsize(destino) == TAMANHO_ESPERADO and _sha256(destino) == SHA256_ESPERADO:
        print(f'{NOME_DO_ARQUIVO}: já presente e confere.')
        return destino

    with abrir(URL, timeout=120) as resposta:
        conteudo = resposta.read(TAMANHO_ESPERADO + 1)

    if len(conteudo) != TAMANHO_ESPERADO:
        raise RuntimeError(f'{NOME_DO_ARQUIVO}: tamanho diferente do esperado ({len(conteudo)}); descartado.')

    with tempfile.NamedTemporaryFile(dir=pasta, delete=False, suffix='.parte') as tmp:
        tmp.write(conteudo)
        caminho_tmp = tmp.name

    if _sha256(caminho_tmp) != SHA256_ESPERADO:
        os.remove(caminho_tmp)
        raise RuntimeError(f'{NOME_DO_ARQUIVO}: o SHA-256 não confere com o esperado; arquivo descartado.')

    os.replace(caminho_tmp, destino)
    print(f'{NOME_DO_ARQUIVO}: baixado e conferido.')
    return destino


if __name__ == '__main__':
    baixar(sys.argv[1] if len(sys.argv) > 1 else pasta_padrao())
