# Configuração do serviço de ML lida do ambiente em um só lugar, para que o
# chatbot, a autenticação e o CORS não tenham cada um a sua cópia.
import os
from urllib.parse import urlparse

# Segredo compartilhado com a Web API .NET (MlApi:InternalToken). Só o backend
# conhece; é o que autoriza as chamadas servidor-a-servidor ao módulo facial.
INTERNAL_TOKEN = os.environ.get('ML_INTERNAL_TOKEN', 'dev-only-shared-secret-troque-em-producao')

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
