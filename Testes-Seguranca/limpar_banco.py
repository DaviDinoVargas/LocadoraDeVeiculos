"""Apaga o banco de TESTE (LocadoraDeVeiculos_Seguranca). Recusa qualquer outro banco.

Usa o sqlcmd quando ele existe (não depende de compilar nada, então funciona com a Web API de uso rodando); sem ele, cai no `dotnet ef`.
A cadeia de conexão vem das user-secrets e nunca é impressa."""
import re
import shutil
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib import ambiente  # noqa: E402

cadeia = ambiente.cadeia_do_banco_de_teste()
if ambiente.BANCO_DE_TESTE.lower() not in cadeia.lower():
    sys.exit('A cadeia não aponta para o banco de teste. Abortando.')


def _valor(*chaves):
    for chave in chaves:
        m = re.search(rf'(?i)(?:^|;)\s*{chave}\s*=\s*([^;]*)', cadeia)
        if m:
            return m.group(1).strip()
    return None


def com_sqlcmd() -> int | None:
    exe = shutil.which('sqlcmd')
    if not exe:
        return None
    servidor = _valor('Server', 'Data Source')
    if not servidor:
        return None
    args = [exe, '-S', servidor, '-b', '-C']
    usuario, senha = _valor('User Id', 'UID', 'User'), _valor('Password', 'PWD')
    if usuario and senha:
        args += ['-U', usuario, '-P', senha]
    else:
        args += ['-E']   # autenticação integrada do Windows
    banco = ambiente.BANCO_DE_TESTE
    # SINGLE_USER WITH ROLLBACK IMMEDIATE derruba conexões que ainda estejam abertas (uma API de teste esquecida, por exemplo)
    comando = (f"IF DB_ID(N'{banco}') IS NOT NULL BEGIN ALTER DATABASE [{banco}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
               f"DROP DATABASE [{banco}]; END")
    r = subprocess.run(args + ['-Q', comando], capture_output=True, text=True)
    saida = (r.stdout + r.stderr)
    for segredo in filter(None, (senha, usuario)):
        saida = saida.replace(segredo, '<oculto>')
    print(f'sqlcmd: {saida.strip()[-300:] or "banco de teste apagado (ou não existia)"}')
    return r.returncode


codigo = com_sqlcmd()
if codigo is None:
    r = subprocess.run(['dotnet', 'ef', 'database', 'drop', '--force', '--connection', cadeia,
                        '--project', str(ambiente.RAIZ / 'Server' / 'LocadoraDeVeiculos.Infraestrutura.Orm'),
                        '--startup-project', str(ambiente.RAIZ / 'Server' / 'web-api'), '--context', 'LocadoraDeVeiculosDbContext'],
                       capture_output=True, text=True, cwd=ambiente.RAIZ)
    print((r.stdout + r.stderr).replace(cadeia, '<cadeia>')[-600:])
    codigo = r.returncode
sys.exit(codigo)
