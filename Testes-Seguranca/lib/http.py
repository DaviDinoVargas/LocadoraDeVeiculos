"""Cliente HTTP mínimo (só biblioteca padrão) para os testes de segurança: não segue redirecionamentos,
não guarda cookies sozinho e devolve tudo o que um atacante enxergaria (status, cabeçalhos, corpo, tempo)."""
import base64
import http.client
import json
import secrets
import ssl
import threading
import time
from dataclasses import dataclass, field
from urllib.parse import urlsplit


@dataclass
class Resposta:
    status: int = 0
    cabecalhos: dict = field(default_factory=dict)  # chaves em minúsculas; Set-Cookie vem em 'set-cookie' (lista unida por \n)
    corpo: bytes = b''
    ms: float = 0.0
    erro: str | None = None  # conexão recusada/encerrada/timeout

    @property
    def texto(self) -> str:
        return self.corpo.decode('utf-8', errors='replace')

    def json(self):
        try:
            return json.loads(self.corpo)
        except (ValueError, TypeError):
            return None

    @property
    def ok(self) -> bool:
        return 200 <= self.status < 300


def id_de(resp: 'Resposta'):
    """Id devolvido por um POST de cadastro (None se a resposta for de erro)."""
    j = resp.json()
    return (j.get('id') or j.get('Id')) if isinstance(j, dict) else None


def ip_unico() -> str:
    """IP de origem fictício (via X-Forwarded-For; a API só confia no proxy local) para que cada teste
    use o próprio balde de rate limit e um teste não derrube o outro."""
    return f"10.{secrets.randbelow(250) + 1}.{secrets.randbelow(250) + 1}.{secrets.randbelow(250) + 1}"


_local = threading.local()


def _nova_conexao(esquema, host, porta, timeout):
    if esquema == 'https':
        # Só contra a API de teste em 127.0.0.1, que usa o certificado de desenvolvimento (não confiável de propósito): sem verificação.
        return http.client.HTTPSConnection(host, porta, timeout=timeout, context=ssl._create_unverified_context())
    return http.client.HTTPConnection(host, porta, timeout=timeout)


def _conexao(host, porta, timeout, reutilizar, esquema='http'):
    if not reutilizar:
        return _nova_conexao(esquema, host, porta, timeout)
    cache = getattr(_local, 'conexoes', None)
    if cache is None:
        cache = _local.conexoes = {}
    chave = (host, porta)
    if chave not in cache:
        cache[chave] = _nova_conexao(esquema, host, porta, timeout)
    return cache[chave]


def _descartar(host, porta):
    cache = getattr(_local, 'conexoes', {})
    c = cache.pop((host, porta), None)
    if c:
        c.close()


def requisitar(metodo: str, url: str, *, json_corpo=None, bruto: bytes | None = None, cabecalhos: dict | None = None,
               token: str | None = None, ip: str | None = None, cookie: str | None = None,
               timeout: float = 30.0, reutilizar: bool = False) -> Resposta:
    partes = urlsplit(url)
    caminho = partes.path + (f'?{partes.query}' if partes.query else '')
    h = {'Accept': 'application/json'}
    corpo = bruto
    if json_corpo is not None:
        corpo = json.dumps(json_corpo).encode('utf-8')
        h['Content-Type'] = 'application/json'
    elif bruto is not None:
        h['Content-Type'] = 'application/json'
    if token:
        h['Authorization'] = f'Bearer {token}'
    if ip:
        h['X-Forwarded-For'] = ip
    if cookie:
        h['Cookie'] = cookie
    h.update(cabecalhos or {})

    resposta = Resposta()
    inicio = time.perf_counter()
    porta = partes.port or (443 if partes.scheme == 'https' else 80)
    for tentativa in range(2 if reutilizar else 1):
        conexao = _conexao(partes.hostname, porta, timeout, reutilizar, partes.scheme)
        try:
            conexao.request(metodo, caminho, body=corpo, headers=h)
            r = conexao.getresponse()
            resposta.status = r.status
            resposta.cabecalhos = {}
            for nome, valor in r.getheaders():
                chave = nome.lower()
                resposta.cabecalhos[chave] = resposta.cabecalhos[chave] + chr(10) + valor if chave in resposta.cabecalhos else valor
            resposta.corpo = r.read()
            resposta.erro = None
            if reutilizar and resposta.cabecalhos.get('connection', '').lower() == 'close':
                _descartar(partes.hostname, porta)
            break
        except (OSError, http.client.HTTPException) as ex:
            resposta.erro = f'{type(ex).__name__}: {ex}'
            if reutilizar:
                _descartar(partes.hostname, porta)  # conexão velha: a próxima tentativa abre outra
            else:
                conexao.close()
    resposta.ms = (time.perf_counter() - inicio) * 1000
    return resposta


def b64url(dados: bytes) -> str:
    return base64.urlsafe_b64encode(dados).rstrip(b'=').decode()


def b64url_decodificar(texto: str) -> bytes:
    return base64.urlsafe_b64decode(texto + '=' * (-len(texto) % 4))


def claims_do_jwt(token: str) -> dict:
    return json.loads(b64url_decodificar(token.split('.')[1]))


def jwt_forjado(token_original: str, *, alterar_claims: dict | None = None, alg: str | None = None,
                assinatura: str | None = None, chave_hs256: bytes | None = None) -> str:
    """Monta um JWT a partir de um válido, alterando claims/algoritmo/assinatura, para provar que a API os recusa."""
    import hashlib
    import hmac
    cab_b64, pay_b64, assin_b64 = token_original.split('.')
    cabecalho = json.loads(b64url_decodificar(cab_b64))
    payload = json.loads(b64url_decodificar(pay_b64))
    if alg:
        cabecalho['alg'] = alg
    payload.update(alterar_claims or {})
    novo_cab = b64url(json.dumps(cabecalho, separators=(',', ':')).encode())
    novo_pay = b64url(json.dumps(payload, separators=(',', ':')).encode())
    if chave_hs256 is not None:
        assin = b64url(hmac.new(chave_hs256, f'{novo_cab}.{novo_pay}'.encode(), hashlib.sha256).digest())
    else:
        assin = assinatura if assinatura is not None else assin_b64
    return f'{novo_cab}.{novo_pay}.{assin}'
