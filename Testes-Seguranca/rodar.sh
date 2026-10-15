#!/usr/bin/env bash
# Testes de segurança (ataques + carga). Uso:
#   ./Testes-Seguranca/rodar.sh ataques|carga|tudo [--sem-ml] [--sem-build]
#   ./Testes-Seguranca/rodar.sh zap [--sem-build] [--minutos N]   # OWASP ZAP (varredura ativa autenticada, ~30 min)
#   ./Testes-Seguranca/rodar.sh comparar      # compara a última execução com a anterior
#   ./Testes-Seguranca/rodar.sh limpar        # apaga o banco de TESTE (LocadoraDeVeiculos_Seguranca)
# Os resultados vão para FORA do repositório (SEGURANCA_RESULTADOS; padrão: ../Seguranca-Resultados).
set -euo pipefail
cd "$(dirname "$0")"
PY="${PYTHON:-python}"
case "${1:-tudo}" in
  comparar) shift; exec "$PY" comparar.py "$@" ;;
  zap)      shift; exec "$PY" -X utf8 zap_dast.py "$@" ;;
  limpar)   exec "$PY" limpar_banco.py ;;
  *)        exec "$PY" -X utf8 executar.py "$@" ;;
esac
