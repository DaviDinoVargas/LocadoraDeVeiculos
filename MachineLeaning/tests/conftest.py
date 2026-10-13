"""Os testes rodam em modo de desenvolvimento do ML. O padrão do serviço é produção (falha fechada): sem ML_ENV
o token interno público é recusado e as câmeras ficam fechadas. Precisa ser definido antes de importar `shared.settings`."""
import os

os.environ.setdefault('ML_ENV', 'development')
