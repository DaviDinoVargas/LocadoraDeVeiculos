"""Cadeia de suprimentos e retenção no ML (S23).

- Os pesos do YOLO (`.pt`) são pickles do PyTorch: carregar um arquivo de origem desconhecida pode
  executar código. Fora de desenvolvimento o hash SHA-256 do arquivo precisa ser informado
  (YOLO_MODEL_SHA256) e é conferido antes de carregar.
- Recortes de placas (`crops/`) são imagens de pessoas e veículos: só existem com SAVE_CROPS ligado
  e são apagados depois de CROPS_RETENTION_HOURS (padrão 24 h).
"""
import hashlib
import logging
import os
import time

log = logging.getLogger(__name__)

_ULTIMA_LIMPEZA: dict[str, float] = {}
_INTERVALO_ENTRE_LIMPEZAS_S = 600.0


def sha256_do_arquivo(caminho: str) -> str:
    h = hashlib.sha256()
    with open(caminho, 'rb') as f:
        for bloco in iter(lambda: f.read(1024 * 1024), b''):
            h.update(bloco)
    return h.hexdigest()


def verificar_peso(caminho: str, sha256_esperado: str | None, em_desenvolvimento: bool) -> None:
    """Levanta RuntimeError se o hash não bater, ou se faltar fora de desenvolvimento."""
    esperado = (sha256_esperado or '').strip().lower()

    if not esperado:
        if em_desenvolvimento:
            return
        raise RuntimeError('YOLO_MODEL_SHA256 precisa ser definido fora do ambiente de desenvolvimento.')

    if not os.path.isfile(caminho):
        raise RuntimeError(f'Peso do modelo não encontrado para conferir o hash: {caminho}')

    obtido = sha256_do_arquivo(caminho)
    if obtido != esperado:
        raise RuntimeError(f'O hash do peso do modelo não confere (esperado {esperado[:12]}…, obtido {obtido[:12]}…).')


def limpar_arquivos_antigos(pasta: str, horas: float, agora: float | None = None) -> int:
    """Apaga arquivos de `pasta` mais velhos que `horas`. Devolve quantos apagou."""
    if horas <= 0 or not os.path.isdir(pasta):
        return 0

    limite = (agora if agora is not None else time.time()) - horas * 3600
    apagados = 0
    for nome in os.listdir(pasta):
        caminho = os.path.join(pasta, nome)
        try:
            if os.path.isfile(caminho) and os.path.getmtime(caminho) < limite:
                os.remove(caminho)
                apagados += 1
        except OSError:
            log.warning('Não foi possível apagar %s', caminho)
    return apagados


def limpar_se_for_hora(pasta: str, horas: float) -> int:
    """Versão barata para chamar a cada gravação: só varre a pasta de tempos em tempos."""
    agora = time.monotonic()
    if agora - _ULTIMA_LIMPEZA.get(pasta, -_INTERVALO_ENTRE_LIMPEZAS_S) < _INTERVALO_ENTRE_LIMPEZAS_S:
        return 0
    _ULTIMA_LIMPEZA[pasta] = agora
    return limpar_arquivos_antigos(pasta, horas)
