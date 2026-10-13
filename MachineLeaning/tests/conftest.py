"""Os testes rodam em modo de desenvolvimento do ML. O padrão do serviço é produção (falha fechada): sem ML_ENV
o token interno público é recusado e as câmeras ficam fechadas. Precisa ser definido antes de importar `shared.settings`."""
import os

os.environ.setdefault('ML_ENV', 'development')
# Os pesos da prova de vida não ficam no git: os testes desligam a checagem real e, onde importa, usam um modelo falso.
os.environ.setdefault('ANTISPOOF_ENABLED', 'false')
