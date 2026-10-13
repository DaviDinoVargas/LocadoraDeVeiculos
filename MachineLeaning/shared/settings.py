# Configuração do serviço de ML lida do ambiente em um só lugar, para que o
# chatbot, a autenticação e o CORS não tenham cada um a sua cópia.
import os
from urllib.parse import urlparse

TOKEN_INTERNO_DE_DESENVOLVIMENTO = 'dev-only-shared-secret-troque-em-producao'
TAMANHO_MINIMO_TOKEN_INTERNO = 32


def em_desenvolvimento(env=os.environ) -> bool:
    """Só ML_ENV=development liga o modo de desenvolvimento. Ausente ou qualquer outro valor conta como ambiente REAL
    (falha fechada: esquecer a variável nunca expõe /docs, o token público nem a escuta aberta)."""
    return env.get('ML_ENV', '').strip().lower() == 'development'


def resolver_token_interno(env=os.environ) -> str:
    """Segredo compartilhado com a Web API .NET (MlApi:InternalToken). Só o backend o conhece; é o que
    autoriza as chamadas servidor-a-servidor ao módulo facial.

    Em desenvolvimento o valor padrão público é aceito por conveniência. Fora dele o serviço se recusa
    a subir sem um segredo próprio e longo, porque quem conhece o padrão (está no repositório) poderia
    cadastrar, usar como oráculo ou apagar rostos."""
    valor = env.get('ML_INTERNAL_TOKEN', '').strip()

    if em_desenvolvimento(env):
        return valor or TOKEN_INTERNO_DE_DESENVOLVIMENTO

    if not valor or valor == TOKEN_INTERNO_DE_DESENVOLVIMENTO:
        raise RuntimeError('ML_INTERNAL_TOKEN precisa ser definido (e diferente do valor de desenvolvimento) fora do ambiente de desenvolvimento.')
    if len(valor) < TAMANHO_MINIMO_TOKEN_INTERNO:
        raise RuntimeError(f'ML_INTERNAL_TOKEN precisa ter pelo menos {TAMANHO_MINIMO_TOKEN_INTERNO} caracteres.')
    return valor


INTERNAL_TOKEN = resolver_token_interno()

# Endereço de escuta: só a própria máquina por padrão. Quem põe o ML atrás de um proxy ou em outro host define HOST.
HOST = os.environ.get('HOST', '127.0.0.1')

# Empresa dona das câmeras físicas deste servidor (S24): o vídeo e as placas lidas são dela, e nenhuma outra conta,
# mesmo autenticada, os enxerga. Em desenvolvimento, sem a variável, qualquer conta autenticada acessa (para trabalhar
# localmente); fora dele, sem a variável ninguém acessa.
CAMERA_EMPRESA_ID = (os.environ.get('CAMERA_EMPRESA_ID') or '').strip().lower() or None

# /docs, /redoc e /openapi.json só existem em desenvolvimento: descrevem toda a superfície do serviço.
DOCS_HABILITADOS = em_desenvolvimento()

DOTNET_API_URL = os.environ.get('DOTNET_API_URL', 'https://localhost:7064')


def _verificar_ssl_padrao() -> bool:
    # Só o ambiente local usa o certificado autoassinado do `dotnet dev-certs`.
    # Qualquer outro host (ex.: um domínio real) tem o certificado verificado.
    host = (urlparse(DOTNET_API_URL).hostname or '').lower()
    return host not in ('localhost', '127.0.0.1', '::1')


_verify = os.environ.get('DOTNET_API_VERIFY_SSL')
VERIFY_SSL = _verificar_ssl_padrao() if _verify is None else _verify.lower() in ('1', 'true', 'yes')

# Origens do Angular autorizadas a chamar o ML direto pelo navegador (CORS).
# Mesmo formato do CORS_ALLOWED_ORIGINS da Web API: separadas por ";" ou ",".
CORS_ORIGINS = [
    o.strip()
    for o in os.environ.get('CORS_ALLOWED_ORIGINS', 'http://localhost:4200;https://localhost:4200')
    .replace(',', ';').split(';')
    if o.strip()
]
