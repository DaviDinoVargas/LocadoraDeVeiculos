"""Sobe e derruba as instâncias de teste (Web API e ML) e prepara os usuários de teste.

Segurança do próprio teste:
- usa um banco SEPARADO (LocadoraDeVeiculos_Seguranca); recusa rodar se isso não puder ser garantido;
- a cadeia de conexão vem das user-secrets e nunca é impressa nem gravada;
- senhas e tokens são gerados na hora, não existem no código;
- os resultados vão para fora do repositório (variável SEGURANCA_RESULTADOS).
"""
import base64
import hashlib
import os
import re
import secrets
import shutil
import socket
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from .http import claims_do_jwt, ip_unico, requisitar

RAIZ = Path(__file__).resolve().parents[2]
BANCO_DE_TESTE = 'LocadoraDeVeiculos_Seguranca'
PORTA_API = 5180          # limites padrão (testes de ataque)
PORTA_API_CARGA = 5181    # limite global alto (testes de carga)
PORTA_ML = 8001
# A API de teste é compilada numa pasta FORA do repositório: assim a Web API que a pessoa deixa rodando para usar o sistema
# (e que trava as DLLs de bin/Debug) não atrapalha os testes, e os testes não derrubam a API de uso.
SAIDA_DO_BUILD = RAIZ.parent / 'Seguranca-Build' / 'web-api'

# Tudo que os testes escrevem em pasta temporária (certificado exportado, perfil do ZAP, arquivos do dotnet/Java/Python) vai para o disco A:,
# não para o TEMP do C:, que é pequeno e já chegou a encher por completo durante uma rodada.
PASTA_TEMP = RAIZ.parent / 'Seguranca-Temp'
PASTA_TEMP.mkdir(parents=True, exist_ok=True)
tempfile.tempdir = str(PASTA_TEMP)


def env_com_temp_no_disco_a(base=None) -> dict:
    env = dict(base if base is not None else os.environ)
    env.update({'TEMP': str(PASTA_TEMP), 'TMP': str(PASTA_TEMP), 'TMPDIR': str(PASTA_TEMP)})
    return env


def pasta_de_resultados() -> Path:
    destino = Path(os.environ.get('SEGURANCA_RESULTADOS') or (RAIZ.parent / 'Seguranca-Resultados')).resolve()
    if RAIZ in destino.parents or destino == RAIZ:
        raise SystemExit('SEGURANCA_RESULTADOS aponta para dentro do repositório: resultados de ataque não podem ir para o git.')
    destino.mkdir(parents=True, exist_ok=True)
    return destino


def commit_atual() -> str:
    r = subprocess.run(['git', 'rev-parse', '--short', 'HEAD'], cwd=RAIZ, capture_output=True, text=True)
    return r.stdout.strip() or 'sem-commit'


def _segredos() -> dict:
    r = subprocess.run(['dotnet', 'user-secrets', 'list', '--project', str(RAIZ / 'Server' / 'web-api')],
                       capture_output=True, text=True, cwd=RAIZ)
    segredos = {}
    for linha in r.stdout.splitlines():
        if ' = ' in linha:
            k, v = linha.split(' = ', 1)
            segredos[k.strip()] = v.strip()
    return segredos


def cadeia_do_banco_de_teste() -> str:
    """Cadeia de conexão das user-secrets trocando só o nome do banco. Nunca é impressa."""
    base = _segredos().get('SQL_CONNECTION_STRING') or _segredos().get('ConnectionStrings:SQL_CONNECTION_STRING')
    if not base:
        raise SystemExit('SQL_CONNECTION_STRING não encontrada nas user-secrets da web-api.')
    nova, n = re.subn(r'(?i)(Database|Initial Catalog)\s*=\s*[^;]+', rf'\1={BANCO_DE_TESTE}', base)
    if n != 1:
        raise SystemExit('Não foi possível trocar o nome do banco na cadeia de conexão.')
    if BANCO_DE_TESTE.lower() not in nova.lower():
        raise SystemExit('Cadeia de teste não aponta para o banco de teste. Abortando por segurança.')
    return nova


def porta_livre(porta: int) -> bool:
    with socket.socket() as s:
        s.settimeout(0.5)
        return s.connect_ex(('127.0.0.1', porta)) != 0


def senha_forte() -> str:
    return 'Aa1!' + secrets.token_urlsafe(12)


def construir_api() -> None:
    print('Compilando a Web API...', flush=True)
    r = subprocess.run(['dotnet', 'build', str(RAIZ / 'Server' / 'web-api'), '-c', 'Debug', '-o', str(SAIDA_DO_BUILD), '-v', 'q', '--nologo'],
                       capture_output=True, text=True, cwd=RAIZ, env=env_com_temp_no_disco_a())
    if r.returncode != 0:
        raise SystemExit('Falha ao compilar a Web API.\n' + r.stdout[-1500:])


class Servico:
    def __init__(self, nome, comando, cwd, env, url_pronto, pasta_logs: Path, timeout=120):
        self.nome, self.comando, self.cwd, self.env, self.url_pronto = nome, comando, cwd, env, url_pronto
        self.caminho_log = pasta_logs / f'{nome}.log'
        self.timeout = timeout
        self.proc = None
        self.pastas_temporarias = []

    def iniciar(self):
        self._log = open(self.caminho_log, 'wb')
        self.proc = subprocess.Popen(self.comando, cwd=self.cwd, env=self.env, stdout=self._log, stderr=subprocess.STDOUT)
        limite = time.time() + self.timeout
        while time.time() < limite:
            if self.proc.poll() is not None:
                raise SystemExit(f'{self.nome} encerrou na subida; veja {self.caminho_log}')
            r = requisitar('GET', self.url_pronto, timeout=3)
            if r.status and r.status < 500:
                print(f'{self.nome} pronto ({self.url_pronto}).', flush=True)
                return
            time.sleep(1)
        self.parar()
        raise SystemExit(f'{self.nome} não ficou pronto em {self.timeout}s; veja {self.caminho_log}')

    def parar(self):
        if self.proc and self.proc.poll() is None:
            subprocess.run(['taskkill', '/PID', str(self.proc.pid), '/T', '/F'], capture_output=True)
            try:
                self.proc.wait(timeout=15)
            except subprocess.TimeoutExpired:
                pass
        if getattr(self, '_log', None):
            self._log.close()
        for pasta in self.pastas_temporarias:
            shutil.rmtree(pasta, ignore_errors=True)

    def memoria_mb(self):
        """Memória residente (MB) do processo, via tasklist (sem dependências)."""
        if not self.proc:
            return None
        r = subprocess.run(['tasklist', '/FI', f'PID eq {self.proc.pid}', '/FO', 'CSV', '/NH'], capture_output=True, text=True)
        m = re.search(r'"([\d.,\s]+) K"\s*$', r.stdout.strip())
        return round(int(re.sub(r'\D', '', m.group(1))) / 1024, 1) if m else None


def iniciar_api(pasta_logs: Path, porta: int, extras: dict | None = None, nome='api') -> Servico:
    if not porta_livre(porta):
        raise SystemExit(f'A porta {porta} já está em uso.')
    env = env_com_temp_no_disco_a()
    env.update({
        'ASPNETCORE_ENVIRONMENT': 'Development',   # as user-secrets (chave JWT etc.) só carregam em Development
        'ASPNETCORE_URLS': f'http://127.0.0.1:{porta}',
        'SQL_CONNECTION_STRING': cadeia_do_banco_de_teste(),
        'CORS_ALLOWED_ORIGINS': 'http://localhost:4200',
        'DOTNET_ENVIRONMENT': 'Development',
    })
    env.update(extras or {})
    api_dir = RAIZ / 'Server' / 'web-api'
    dll = SAIDA_DO_BUILD / 'LocadoraDeVeiculos.WebApi.dll'
    return Servico(nome, ['dotnet', str(dll)], api_dir, env, f'http://127.0.0.1:{porta}/api/auth/sessao', pasta_logs)


PORTA_API_PRODUCAO_HTTP = 5182
PORTA_API_PRODUCAO_HTTPS = 5183
HOST_PUBLICO_DE_TESTE = 'api.locadora.teste'   # nome "de verdade": o HSTS do ASP.NET não é emitido para localhost/127.0.0.1


def iniciar_api_producao(pasta_logs: Path, segredos_gerados: list, nome='api-producao') -> Servico:
    """A API em modo PRODUÇÃO (ASPNETCORE_ENVIRONMENT=Production) e com HTTPS de verdade (Kestrel com certificado), nas portas 5182 (http, só
    para redirecionar) e 5183 (https). É como ela se comporta fora do desenvolvimento: HSTS, sem Swagger, erros genéricos, cookie Secure.
    Usa o banco de TESTE; os segredos vêm das user-secrets para variáveis de ambiente do processo e nunca são impressos."""
    for porta in (PORTA_API_PRODUCAO_HTTP, PORTA_API_PRODUCAO_HTTPS):
        if not porta_livre(porta):
            raise SystemExit(f'A porta {porta} já está em uso.')

    pasta_cert = Path(tempfile.mkdtemp(prefix='locadora-cert-'))
    pfx, senha_pfx = pasta_cert / 'dev-cert.pfx', secrets.token_urlsafe(16)
    r = subprocess.run(['dotnet', 'dev-certs', 'https', '-ep', str(pfx), '-p', senha_pfx], capture_output=True, text=True)
    if r.returncode != 0 or not pfx.exists():
        shutil.rmtree(pasta_cert, ignore_errors=True)
        raise SystemExit('Não foi possível exportar o certificado de desenvolvimento (dotnet dev-certs https).')

    chave_facial = '1:' + base64.b64encode(secrets.token_bytes(32)).decode()
    token_interno = secrets.token_urlsafe(40)   # em produção a API recusa o token padrão de desenvolvimento (S2)
    segredos_gerados.extend([senha_pfx, chave_facial, token_interno])
    env = env_com_temp_no_disco_a()
    for chave, valor in _segredos().items():
        env[chave.replace(':', '__')] = valor
    env.update({
        'ASPNETCORE_ENVIRONMENT': 'Production',
        'DOTNET_ENVIRONMENT': 'Production',
        'ASPNETCORE_URLS': f'http://127.0.0.1:{PORTA_API_PRODUCAO_HTTP};https://127.0.0.1:{PORTA_API_PRODUCAO_HTTPS}',
        'ASPNETCORE_HTTPS_PORT': str(PORTA_API_PRODUCAO_HTTPS),
        'ASPNETCORE_Kestrel__Certificates__Default__Path': str(pfx),
        'ASPNETCORE_Kestrel__Certificates__Default__Password': senha_pfx,
        'SQL_CONNECTION_STRING': cadeia_do_banco_de_teste(),
        'FACE_ENCRYPTION_KEYS': chave_facial,
        'MlApi__InternalToken': token_interno,
        'AllowedHosts': f'{HOST_PUBLICO_DE_TESTE};127.0.0.1;localhost',
        'CORS_ALLOWED_ORIGINS': 'http://localhost:4200',
    })
    api_dir = RAIZ / 'Server' / 'web-api'
    servico = Servico(nome, ['dotnet', str(SAIDA_DO_BUILD / 'LocadoraDeVeiculos.WebApi.dll')], api_dir, env,
                      f'https://127.0.0.1:{PORTA_API_PRODUCAO_HTTPS}/api/auth/sessao', pasta_logs)
    servico.pastas_temporarias.append(pasta_cert)
    return servico


def sha256_do_peso() -> str | None:
    peso = RAIZ / 'MachineLeaning' / 'yolov8n.pt'
    if not peso.exists():
        return None
    h = hashlib.sha256()
    h.update(peso.read_bytes())
    return h.hexdigest()


def iniciar_ml(pasta_logs: Path, token_interno: str, url_api: str, empresa_das_cameras: str | None = None) -> Servico:
    """ML em modo de ambiente real (ML_ENV=production): é como ele precisa se comportar fora do desenvolvimento."""
    if not porta_livre(PORTA_ML):
        raise SystemExit(f'A porta {PORTA_ML} já está em uso.')
    env = env_com_temp_no_disco_a()
    env.update({
        'ML_ENV': 'production',
        'ML_INTERNAL_TOKEN': token_interno,
        'DOTNET_API_URL': url_api,
        'DOTNET_API_VERIFY_SSL': 'false',
        'CORS_ALLOWED_ORIGINS': 'http://localhost:4200',
        'PYTHONIOENCODING': 'utf-8',
    })
    if empresa_das_cameras:
        env['CAMERA_EMPRESA_ID'] = empresa_das_cameras
    sha = sha256_do_peso()
    if sha:
        env['YOLO_MODEL_SHA256'] = sha
    return Servico('ml', [sys.executable, '-m', 'uvicorn', 'main:app', '--host', '127.0.0.1', '--port', str(PORTA_ML)],
                   RAIZ / 'MachineLeaning', env, f'http://127.0.0.1:{PORTA_ML}/health', pasta_logs, timeout=180)


class Conta:
    """Uma empresa (ou funcionário) de teste já autenticada."""

    def __init__(self, base, email, senha, token, cookie_refresh, tipo):
        self.base, self.email, self.senha, self.token, self.cookie, self.tipo = base, email, senha, token, cookie_refresh, tipo

    @property
    def id(self):
        return claims_do_jwt(self.token)['sub']

    @property
    def empresa_id(self):
        return claims_do_jwt(self.token).get('EmpresaId')


def _extrair_cookie_refresh(set_cookie: str) -> str | None:
    m = re.search(r'(LocadoraDeVeiculos\.RefreshToken=[^;]+)', set_cookie or '')
    return m.group(1) if m else None


def nova_empresa(base: str, prefixo='emp') -> Conta:
    email = f'{prefixo}-{secrets.token_hex(5)}@teste.local'
    senha = senha_forte()
    r = requisitar('POST', f'{base}/api/auth/registrar', ip=ip_unico(),
                   json_corpo={'nomeCompleto': f'Empresa {prefixo}', 'email': email, 'senha': senha, 'confirmarSenha': senha})
    if not r.ok:
        raise RuntimeError(f'Falha ao registrar conta de teste: {r.status} {r.texto[:200]}')
    return Conta(base, email, senha, r.json()['chave'], _extrair_cookie_refresh(r.cabecalhos.get('set-cookie', '')), 'Empresa')


def login(base: str, email: str, senha: str, ip: str | None = None, reutilizar: bool = False):
    return requisitar('POST', f'{base}/api/auth/autenticar', ip=ip or ip_unico(), json_corpo={'email': email, 'senha': senha},
                      reutilizar=reutilizar)


def conta_do_login(base, email, senha, tipo) -> Conta:
    r = login(base, email, senha)
    if not r.ok:
        raise RuntimeError(f'Falha ao autenticar conta de teste: {r.status} {r.texto[:200]}')
    return Conta(base, email, senha, r.json()['chave'], _extrair_cookie_refresh(r.cabecalhos.get('set-cookie', '')), tipo)


def cpf_valido() -> str:
    d = [secrets.randbelow(10) for _ in range(9)]
    if len(set(d)) == 1:
        d[0] = (d[0] + 1) % 10
    for tam in (9, 10):
        pesos = range(tam + 1, 1, -1)
        soma = sum(x * p for x, p in zip(d, pesos))
        resto = soma % 11
        d.append(0 if resto < 2 else 11 - resto)
    s = ''.join(map(str, d))
    return f'{s[:3]}.{s[3:6]}.{s[6:9]}-{s[9:]}'


def cnpj_valido() -> str:
    d = [secrets.randbelow(10) for _ in range(8)] + [0, 0, 0, 1]
    for pesos in ([5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2], [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]):
        soma = sum(x * p for x, p in zip(d, pesos))
        resto = soma % 11
        d.append(0 if resto < 2 else 11 - resto)
    s = ''.join(map(str, d))
    return f'{s[:2]}.{s[2:5]}.{s[5:8]}/{s[8:12]}-{s[12:]}'
