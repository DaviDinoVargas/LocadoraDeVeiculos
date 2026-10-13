"""Limite de requisições em memória para o ML (S10): janela fixa por chave (usuário ou IP).

Um processo só: com mais instâncias do ML o limite vale por instância. Serve para barrar abuso de endpoints caros
(chat, módulo facial) sem depender de um proxy na frente."""
import threading
import time

from fastapi import HTTPException, Request

_lock = threading.Lock()
_janelas: dict[tuple[str, str], tuple[float, int]] = {}


def _consumir(nome: str, chave: str, maximo: int, janela_s: float) -> bool:
    agora = time.monotonic()
    with _lock:
        if len(_janelas) > 5000:  # evita crescer sem limite sob varredura de chaves
            for k in [k for k, (inicio, _) in _janelas.items() if agora - inicio >= janela_s]:
                del _janelas[k]
        inicio, usados = _janelas.get((nome, chave), (agora, 0))
        if agora - inicio >= janela_s:
            inicio, usados = agora, 0
        if usados >= maximo:
            _janelas[(nome, chave)] = (inicio, usados)
            return False
        _janelas[(nome, chave)] = (inicio, usados + 1)
        return True


def limitar(nome: str, maximo: int, janela_s: float = 60.0):
    """Dependência do FastAPI: responde 429 depois de `maximo` pedidos na janela, por usuário (ou IP)."""
    def dependencia(request: Request) -> None:
        identidade = getattr(request.state, 'usuario', None)
        usuario_id = getattr(identidade, 'usuario_id', None)
        chave = usuario_id or (request.client.host if request.client else 'desconhecido')
        if not _consumir(nome, chave, maximo, janela_s):
            raise HTTPException(status_code=429, detail="Muitas requisições. Tente novamente em instantes.")
    return dependencia


def zerar() -> None:
    with _lock:
        _janelas.clear()
