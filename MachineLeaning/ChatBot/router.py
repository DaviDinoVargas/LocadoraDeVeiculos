from fastapi import APIRouter, Request
from pydantic import BaseModel

from .chatbot_service import responder

router = APIRouter(tags=["chat"])


class MensagemRequest(BaseModel):
    message: str


class MensagemResponse(BaseModel):
    reply: str


def _extrair_bearer_token(request: Request) -> str | None:
    header = request.headers.get('authorization') or request.headers.get('Authorization')
    if not header or not header.lower().startswith('bearer '):
        return None
    return header.split(' ', 1)[1]


@router.post("/chat", response_model=MensagemResponse)
async def conversar(request: Request, body: MensagemRequest):
    token = _extrair_bearer_token(request)
    resposta = await responder(body.message, token)
    return MensagemResponse(reply=resposta)
