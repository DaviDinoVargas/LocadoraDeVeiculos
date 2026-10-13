# Autenticação dos endpoints do serviço de ML.
#
# O ML não emite nem assina tokens: quem autentica de verdade é a Web API .NET.
# Aqui há duas formas de provar quem chama:
#   1. Token interno (X-Internal-Token): chamadas servidor-a-servidor da Web API
#      (módulo facial). O navegador nunca recebe esse segredo.
#   2. JWT do usuário logado (Authorization: Bearer): usado pelo Angular nas
#      câmeras e no chat. O ML não conhece a chave de assinatura -- pergunta à
#      Web API (GET /api/auth/sessao) se o token é válido e QUEM é (empresa e
#      papel), e guarda a resposta por alguns segundos para não consultar a
#      cada requisição.
# O <img> do stream MJPEG não consegue enviar cabeçalho Authorization, então o
# stream usa um ticket de uso único e vida curta, emitido a quem tem JWT e
# ligado à identidade dele.
#
# Isolamento por empresa (S24): as câmeras são dispositivos físicos deste servidor e pertencem a UMA empresa
# (CAMERA_EMPRESA_ID). Estar autenticado não basta para vê-las; é preciso ser dessa empresa.
import hashlib
import hmac
import secrets
import threading
import time
from dataclasses import dataclass

import httpx
from fastapi import Header, HTTPException, Query, Request

from . import settings

TTL_CACHE_SESSAO_S = 30.0
TTL_TICKET_STREAM_S = 30.0


@dataclass(frozen=True)
class Usuario:
    """Quem a Web API diz que é o dono do JWT."""
    usuario_id: str | None = None
    empresa_id: str | None = None
    cargo: str | None = None


_lock = threading.Lock()
_sessoes_validas: dict[str, tuple[float, Usuario]] = {}
_tickets_stream: dict[str, tuple[float, Usuario]] = {}


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


def _sessao_em_cache(chave: str) -> Usuario | None:
    agora = time.monotonic()
    with _lock:
        entrada = _sessoes_validas.get(chave)
        if entrada is not None and entrada[0] > agora:
            return entrada[1]
        _sessoes_validas.pop(chave, None)
        return None


def _guardar_sessao(chave: str, usuario: Usuario) -> None:
    agora = time.monotonic()
    with _lock:
        for k in [k for k, (validade, _) in _sessoes_validas.items() if validade <= agora]:
            del _sessoes_validas[k]
        _sessoes_validas[chave] = (agora + TTL_CACHE_SESSAO_S, usuario)


def _usuario_da_resposta(resposta: httpx.Response) -> Usuario:
    try:
        dados = resposta.json()
    except ValueError:
        return Usuario()
    if not isinstance(dados, dict):
        return Usuario()
    minusculo = lambda v: str(v).strip().lower() if v else None  # noqa: E731
    return Usuario(usuario_id=minusculo(dados.get('usuarioId')), empresa_id=minusculo(dados.get('empresaId')),
                   cargo=str(dados['cargo']) if dados.get('cargo') else None)


async def exigir_usuario(request: Request, authorization: str | None = Header(default=None)) -> Usuario:
    """Aceita a requisição só se o JWT for válido na Web API .NET e devolve quem é o usuário."""
    token = _extrair_bearer(authorization)
    if token is None:
        raise HTTPException(status_code=401, detail="Autenticação necessária")

    chave = _chave_cache(token)
    usuario = _sessao_em_cache(chave)

    if usuario is None:
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

        usuario = _usuario_da_resposta(resposta)
        _guardar_sessao(chave, usuario)

    request.state.usuario = usuario
    return usuario


def _pertence_a_empresa_das_cameras(usuario: Usuario) -> bool:
    dona = settings.CAMERA_EMPRESA_ID
    if dona is None:
        # Sem empresa configurada: desenvolvimento libera qualquer conta autenticada; fora dele ninguém entra.
        return settings.em_desenvolvimento()
    return usuario.empresa_id == dona


async def exigir_dono_das_cameras(request: Request, authorization: str | None = Header(default=None)) -> Usuario:
    """Autenticado E da empresa dona das câmeras deste servidor."""
    usuario = await exigir_usuario(request, authorization)
    if not _pertence_a_empresa_das_cameras(usuario):
        raise HTTPException(status_code=403, detail="Esta conta não tem acesso às câmeras deste servidor")
    return usuario


def emitir_ticket_stream(usuario: Usuario) -> dict:
    ticket = secrets.token_urlsafe(24)
    with _lock:
        agora = time.monotonic()
        for k in [k for k, (validade, _) in _tickets_stream.items() if validade <= agora]:
            del _tickets_stream[k]
        _tickets_stream[ticket] = (agora + TTL_TICKET_STREAM_S, usuario)
    return {"ticket": ticket, "expiraEmSegundos": int(TTL_TICKET_STREAM_S)}


def consumir_ticket_stream(ticket: str | None = Query(default=None)) -> None:
    """Dependência do /stream: o ticket vale uma única conexão e só para a empresa que o pediu."""
    if not ticket:
        raise HTTPException(status_code=401, detail="Ticket de stream ausente")
    with _lock:
        entrada = _tickets_stream.pop(ticket, None)
    if entrada is None or entrada[0] <= time.monotonic():
        raise HTTPException(status_code=401, detail="Ticket de stream inválido ou expirado")
    if not _pertence_a_empresa_das_cameras(entrada[1]):
        raise HTTPException(status_code=403, detail="Esta conta não tem acesso às câmeras deste servidor")
