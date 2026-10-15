from dataclasses import dataclass, field

from .registro import Registro


@dataclass
class Contexto:
    base: str                       # Web API de teste (limites padrão)
    reg: Registro
    ml: str | None = None           # ML de teste (ML_ENV=production)
    token_ml: str | None = None
    ml_servico: object | None = None        # o processo do ML de teste (para medir memória durante os ataques)
    dona_das_cameras: object | None = None  # Conta da empresa dona das câmeras do ML de teste (CAMERA_EMPRESA_ID)
    pasta: object | None = None            # pasta da rodada (api.log, ml.log)
    chave_jwt: str | None = None            # chave de assinatura da API de TESTE (user-secrets): só em memória, nunca impressa
    segredos_gerados: list = field(default_factory=list)  # senhas/tokens usados: nunca podem aparecer nos logs

    def lembrar(self, *valores):
        self.segredos_gerados.extend(v for v in valores if v)
