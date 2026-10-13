from fastapi import APIRouter, Depends, Request
from pydantic import BaseModel, Field

from shared import auth, limites

from .chatbot_service import responder

router = APIRouter(tags=["chat"])


class MensagemRequest(BaseModel):
    message: str = Field(max_length=2000)


class MensagemResponse(BaseModel):
    reply: str


def _extrair_bearer_token(request: Request) -> str | None:
    header = request.headers.get('authorization') or request.headers.get('Authorization')
    if not header or not header.lower().startswith('bearer '):
        return None
    return header.split(' ', 1)[1]


# Login obrigatório e limite por usuário: o chat dispara consultas à Web API em nome de quem pergunta (S10).
@router.post("/chat", response_model=MensagemResponse,
             dependencies=[Depends(auth.exigir_usuario), Depends(limites.limitar("chat", maximo=30, janela_s=60))])
async def conversar(request: Request, body: MensagemRequest):
    token = _extrair_bearer_token(request)
    resposta = await responder(body.message, token)
    return MensagemResponse(reply=resposta)
