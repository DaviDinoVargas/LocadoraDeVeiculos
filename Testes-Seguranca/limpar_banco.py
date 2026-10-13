"""Apaga o banco de TESTE (LocadoraDeVeiculos_Seguranca). Recusa qualquer outro banco."""
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from lib import ambiente  # noqa: E402

cadeia = ambiente.cadeia_do_banco_de_teste()
if ambiente.BANCO_DE_TESTE.lower() not in cadeia.lower():
    sys.exit('A cadeia não aponta para o banco de teste. Abortando.')
r = subprocess.run(['dotnet', 'ef', 'database', 'drop', '--force', '--connection', cadeia,
                    '--project', str(ambiente.RAIZ / 'Server' / 'LocadoraDeVeiculos.Infraestrutura.Orm'),
                    '--startup-project', str(ambiente.RAIZ / 'Server' / 'web-api'), '--context', 'LocadoraDeVeiculosDbContext'],
                   capture_output=True, text=True, cwd=ambiente.RAIZ)
print((r.stdout + r.stderr).replace(cadeia, '<cadeia>')[-600:])
sys.exit(r.returncode)
