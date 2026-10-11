# Cliente HTTP para consultar a Web API .NET (a fonte de verdade dos
# dados) em nome do usuário logado no Angular. O token JWT do usuário
# chega no header Authorization da requisição ao /chat e é repassado
# como está — o chatbot nunca autentica sozinho nem guarda credenciais.
import httpx

from shared import settings

DOTNET_API_URL = settings.DOTNET_API_URL
VERIFY_SSL = settings.VERIFY_SSL


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


async def obter_resumo_dashboard(bearer_token: str | None):
    return await _get('/api/dashboard/resumo', bearer_token)


async def listar_cupons(bearer_token: str | None):
    dados = await _get('/api/cupons', bearer_token)
    return dados.get('registros', dados.get('Registros', []))
