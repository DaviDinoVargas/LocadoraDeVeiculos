# Persistência das amostras de landmarks faciais cadastradas.
#
# Cada pessoa (identificada por um personId vindo do sistema .NET, por
# exemplo "cliente:<guid>" ou "funcionario:<guid>") pode ter várias
# amostras salvas, para tolerar variação de ângulo/expressão.
import json
import os
import threading

_LOCK = threading.Lock()
_DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'data')
_DATA_FILE = os.path.join(_DATA_DIR, 'faces.json')


def _garantir_arquivo():
    os.makedirs(_DATA_DIR, exist_ok=True)
    if not os.path.exists(_DATA_FILE):
        with open(_DATA_FILE, 'w', encoding='utf-8') as f:
            json.dump({}, f)


def _ler_tudo():
    _garantir_arquivo()
    with open(_DATA_FILE, 'r', encoding='utf-8') as f:
        return json.load(f)


def _escrever_tudo(dados):
    with open(_DATA_FILE, 'w', encoding='utf-8') as f:
        json.dump(dados, f)


def salvar_amostra(person_id: str, landmarks_normalizados, max_amostras: int = 3):
    with _LOCK:
        dados = _ler_tudo()
        amostras = dados.get(person_id, [])
        amostras.append(landmarks_normalizados)
        amostras = amostras[-max_amostras:]
        dados[person_id] = amostras
        _escrever_tudo(dados)
        return len(amostras)


def carregar_amostras(person_id: str):
    with _LOCK:
        dados = _ler_tudo()
        return dados.get(person_id, [])


def esta_cadastrado(person_id: str) -> bool:
    with _LOCK:
        dados = _ler_tudo()
        return bool(dados.get(person_id))


def remover_pessoa(person_id: str) -> bool:
    with _LOCK:
        dados = _ler_tudo()
        if person_id in dados:
            del dados[person_id]
            _escrever_tudo(dados)
            return True
        return False
