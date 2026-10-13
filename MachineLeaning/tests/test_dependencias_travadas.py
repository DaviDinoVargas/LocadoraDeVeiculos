"""S23: as dependências do ML estão travadas com hash SHA-256 e o lock acompanha o requirements.txt."""
import os
import re

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))


def _ler(nome):
    with open(os.path.join(RAIZ, nome), encoding='utf-8') as f:
        return f.read()


def _pinos(texto):
    """{nome: versão} das linhas 'pacote==versão' (sem comentários nem extras como uvicorn[standard])."""
    pinos = {}
    for linha in texto.splitlines():
        m = re.match(r'^([A-Za-z0-9_.\-]+)(?:\[[^\]]+\])?==([^\s;\\]+)', linha.strip())
        if m:
            pinos[m.group(1).lower().replace('_', '-')] = m.group(2)
    return pinos


def _blocos_do_lock(texto):
    """Cada pacote do lock com a lista dos seus hashes (um pacote pode aparecer mais de uma vez, por marcador)."""
    blocos, atual = [], None
    for linha in texto.splitlines():
        m = re.match(r'^([A-Za-z0-9_.\-]+)==([^\s;\\]+)', linha)
        if m:
            atual = {'nome': m.group(1).lower().replace('_', '-'), 'versao': m.group(2), 'hashes': []}
            blocos.append(atual)
        elif atual is not None and '--hash=sha256:' in linha:
            atual['hashes'].append(re.search(r'--hash=sha256:([0-9a-f]{64})', linha).group(1))
    return blocos


def test_lock_tem_a_mesma_versao_de_cada_pacote_do_requirements():
    esperados = {k: v for k, v in _pinos(_ler('requirements.txt')).items()}
    no_lock = {}
    for b in _blocos_do_lock(_ler('requirements.lock.txt')):
        no_lock.setdefault(b['nome'], set()).add(b['versao'])

    divergentes = {k: (v, no_lock.get(k)) for k, v in esperados.items() if v not in no_lock.get(k, set())}

    assert not divergentes, f'requirements.txt mudou e o lock não foi regerado: {divergentes}'


def test_todo_pacote_do_lock_tem_pelo_menos_um_hash_sha256_valido():
    for nome in ('requirements.lock.txt', 'requirements-dev.lock.txt'):
        blocos = _blocos_do_lock(_ler(nome))
        assert len(blocos) > 20, nome
        sem_hash = [b['nome'] for b in blocos if not b['hashes']]
        assert not sem_hash, f'{nome}: pacotes sem hash: {sem_hash}'


def test_lock_de_desenvolvimento_inclui_tudo_do_lock_de_producao():
    producao = {(b['nome'], b['versao']) for b in _blocos_do_lock(_ler('requirements.lock.txt'))}
    desenvolvimento = {(b['nome'], b['versao']) for b in _blocos_do_lock(_ler('requirements-dev.lock.txt'))}

    assert producao <= desenvolvimento
    assert ('pytest', '8.3.4') in desenvolvimento and ('pytest', '8.3.4') not in producao


def test_nenhum_caminho_da_maquina_vaza_para_o_lock():
    for nome in ('requirements.lock.txt', 'requirements-dev.lock.txt'):
        texto = _ler(nome)
        assert 'C:/Users' not in texto and 'C:\\Users' not in texto, nome
