# Cliente HTTP para consultar a Web API .NET (a fonte de verdade dos
# dados) em nome do usuário logado no Angular. O token JWT do usuário
# chega no header Authorization da requisição ao /chat e é repassado
# como está — o chatbot nunca autentica sozinho nem guarda credenciais.
import os

import httpx

DOTNET_API_URL = os.environ.get('DOTNET_API_URL', 'https://localhost:7064')

# O ambiente de desenvolvimento usa o certificado HTTPS autoassinado
# gerado pelo `dotnet dev-certs`, que não é confiável para um cliente
# HTTP genérico. Fora de desenvolvimento local, DOTNET_API_VERIFY_SSL=true
# deve ser usado com um certificado válido.
VERIFY_SSL = os.environ.get('DOTNET_API_VERIFY_SSL', 'false').lower() in ('1', 'true', 'yes')


class DotnetApiError(Exception):
    def __init__(self, status_code: int, detail: str):
        self.status_code = status_code
        self.detail = detail
        super().__init__(detail)


async def _get(path: str, bearer_token: str | None):
    headers = {'Authorization': f'Bearer {bearer_token}'} if bearer_token else {}
    try:
        async with httpx.AsyncClient(base_url=DOTNET_API_URL, verify=VERIFY_SSL, timeout=10.0) as client:
            resposta = await client.get(path, headers=headers)
    except httpx.HTTPError as e:
        raise DotnetApiError(503, "sistema principal (Web API) está fora do ar no momento") from e

    if resposta.status_code == 401:
        raise DotnetApiError(401, "Sessão expirada, faça login novamente para consultar esses dados")
    if resposta.status_code >= 400:
        raise DotnetApiError(resposta.status_code, f"Erro ao consultar {path}: {resposta.status_code}")
    return resposta.json()


async def listar_automoveis(bearer_token: str | None):
    dados = await _get('/api/automoveis', bearer_token)
    return dados.get('registros', dados.get('Registros', []))


async def listar_alugueis_em_aberto(bearer_token: str | None):
    dados = await _get('/api/alugueis/em-aberto', bearer_token)
    return dados.get('registros', dados.get('Registros', []))
