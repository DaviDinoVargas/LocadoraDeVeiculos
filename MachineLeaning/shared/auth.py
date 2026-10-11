# Autenticação dos endpoints do serviço de ML.
#
# O ML não emite nem assina tokens: quem autentica de verdade é a Web API .NET.
# Aqui há duas formas de provar quem chama:
#   1. Token interno (X-Internal-Token): chamadas servidor-a-servidor da Web API
#      (módulo facial). O navegador nunca recebe esse segredo.
#   2. JWT do usuário logado (Authorization: Bearer): usado pelo Angular nas
#      câmeras e no chat. O ML não conhece a chave de assinatura -- pergunta à
#      Web API (GET /api/auth/sessao) se o token é válido e guarda o "sim" por
#      alguns segundos para não consultar a cada requisição.
# O <img> do stream MJPEG não consegue enviar cabeçalho Authorization, então o
# stream usa um ticket de uso único e vida curta, emitido a quem tem JWT.
import hashlib
import hmac
import secrets
import threading
import time

import httpx
from fastapi import Header, HTTPException, Query

from . import settings

TTL_CACHE_SESSAO_S = 30.0
TTL_TICKET_STREAM_S = 30.0

_lock = threading.Lock()
_sessoes_validas: dict[str, float] = {}
_tickets_stream: dict[str, float] = {}


def exigir_token_interno(x_internal_token: str | None = Header(default=None)) -> None:
    esperado = settings.INTERNAL_TOKEN
    if not x_internal_token or not hmac.compare_digest(x_internal_token, esperado):
        raise HTTPException(status_code=401, detail="Token interno inválido ou ausente")


def _extrair_bearer(authorization: str | None) -> str | None:
    if not authorization or not authorization.lower().startswith('bearer '):
        return None
    token = authorization.split(' ', 1)[1].strip()
    return token or None


def _chave_cache(token: str) -> str:
    return hashlib.sha256(token.encode('utf-8')).hexdigest()


def _sessao_em_cache(chave: str) -> bool:
    agora = time.monotonic()
    with _lock:
        validade = _sessoes_validas.get(chave)
        if validade is not None and validade > agora:
            return True
        _sessoes_validas.pop(chave, None)
        return False


def _guardar_sessao(chave: str) -> None:
    agora = time.monotonic()
    with _lock:
        for k in [k for k, v in _sessoes_validas.items() if v <= agora]:
            del _sessoes_validas[k]
        _sessoes_validas[chave] = agora + TTL_CACHE_SESSAO_S


async def exigir_usuario(authorization: str | None = Header(default=None)) -> None:
    """Aceita a requisição só se o JWT for válido na Web API .NET."""
    token = _extrair_bearer(authorization)
    if token is None:
        raise HTTPException(status_code=401, detail="Autenticação necessária")

    chave = _chave_cache(token)
    if _sessao_em_cache(chave):
        return

    try:
        async with httpx.AsyncClient(
            base_url=settings.DOTNET_API_URL, verify=settings.VERIFY_SSL, timeout=5.0
        ) as client:
            resposta = await client.get('/api/auth/sessao', headers={'Authorization': f'Bearer {token}'})
    except httpx.HTTPError:
        raise HTTPException(status_code=503, detail="Não foi possível validar a sessão: Web API fora do ar")

    if resposta.status_code in (401, 403):
        raise HTTPException(status_code=401, detail="Sessão inválida ou expirada")
    if resposta.status_code >= 400:
        raise HTTPException(status_code=503, detail="Não foi possível validar a sessão")

    _guardar_sessao(chave)


def emitir_ticket_stream() -> dict:
    ticket = secrets.token_urlsafe(24)
    with _lock:
        agora = time.monotonic()
        for k in [k for k, v in _tickets_stream.items() if v <= agora]:
            del _tickets_stream[k]
        _tickets_stream[ticket] = agora + TTL_TICKET_STREAM_S
    return {"ticket": ticket, "expiraEmSegundos": int(TTL_TICKET_STREAM_S)}


def consumir_ticket_stream(ticket: str | None = Query(default=None)) -> None:
    """Dependência do /stream: o ticket vale uma única conexão."""
    if not ticket:
        raise HTTPException(status_code=401, detail="Ticket de stream ausente")
    with _lock:
        validade = _tickets_stream.pop(ticket, None)
    if validade is None or validade <= time.monotonic():
        raise HTTPException(status_code=401, detail="Ticket de stream inválido ou expirado")
