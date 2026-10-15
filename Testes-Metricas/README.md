# Testes-Metricas — números repetíveis para a documentação

Mede o app **como ele iria para produção**: build de produção do Angular servido por um proxy que faz o papel do nginx,
contra uma Web API real e um banco **separado** (`LocadoraDeVeiculos_Metricas`, nunca o banco de uso). Tudo é descartável:
cada execução cria uma empresa de teste nova (e-mail `@teste.local`, senha gerada na hora, nunca impressa nem gravada) e
semeia dados de exemplo pela própria API (grupos, planos, 13 veículos, 8 clientes e condutores, aluguéis reservados, em
andamento e concluídos com devolução).

Os **resultados ficam fora do repositório** (`../Metricas-Resultados`, ou `METRICAS_RESULTADOS`); os scripts ficam aqui.

## Como rodar

```bash
cd Testes-Metricas
npm install                                   # uma vez: Lighthouse, Playwright, axe-core (não mexe no Client)
python orquestrar.py pacote                   # tamanho do pacote (rápido, sem subir a API)
python orquestrar.py lighthouse               # Lighthouse nas telas-chave, desktop e celular (3 rodadas, mediana)
python orquestrar.py e2e                      # fluxos de ponta a ponta + axe-core na página inteira
python orquestrar.py ml                       # tempo do OCR de placa (placas sintéticas; precisa do Tesseract)
python orquestrar.py carga                    # tempos da API (p50/p95/p99), pela bateria de carga da segurança
python orquestrar.py tudo                     # lighthouse + e2e (+ pacote)
python orquestrar.py manter                   # só sobe o ambiente (API + front + dados), para depurar à mão
```

Opções: `--sem-build-api`, `--sem-build-front` (reaproveitam o último build). Variáveis úteis: `METRICAS_RODADAS` (rodadas por tela
no Lighthouse, padrão 3), `METRICAS_TELAS` (ex.: `home,alugueis`), `METRICAS_E2E_ARGS` (argumentos do Playwright, ex.:
`fluxos.spec.mjs`), `EDGE_PATH`. Requisitos: Node ≥ 20, Python 3.11, .NET 8, SQL Server com as user-secrets da web-api, Microsoft Edge instalado.

## O que cada medição entrega

| Medição | Onde | Saída |
|---|---|---|
| Tamanho do pacote (inicial x sob demanda, bruto e gzip) | `apoio/pacote.py` | `pacote.md` / `pacote.json` |
| Lighthouse (Desempenho, Acessibilidade, Boas práticas; FCP, LCP, CLS, TBT, Speed Index, KB transferidos), desktop e celular, com a lista de auditorias que falham e as maiores oportunidades | `lighthouse/executar.mjs` | `lighthouse/lighthouse.md`, `.json` e um relatório HTML por tela |
| Fluxos críticos cronometrados (cadastrar cliente, criar aluguel, registrar devolução): tempo, cliques, preenchimentos, erro | `e2e/fluxos.spec.mjs` | `e2e/resumo.md`, `tarefas.jsonl` |
| axe-core na página inteira (11 telas, temas claro e escuro) | `e2e/acessibilidade.spec.mjs` | `e2e/resumo.md`, `acessibilidade.jsonl` |
| Tempo de leitura de placa (OCR sobre placas sintéticas; sem YOLO nem câmera real; o acerto não é reportado) | `ml/tempo_leitura_placa.py` | `ml/leitura_placa.md`, `.json` |
| Tempos da API (p50/p95/p99 por cenário) | `../Testes-Seguranca` (modo `carga`) | `Seguranca-Resultados/.../resumo.md` |

Em paralelo, o axe-core também roda **dentro dos specs do Angular** (`Client`, `ng test`): um teste por tela-chave, nos dois temas,
no Edge real (então contraste e nome acessível valem). Os dois se complementam: o do Karma audita componentes isolados; o do
Playwright audita a página montada (landmarks, título, idioma).

## Como ler os números (limites honestos)

- **Lighthouse é medição de laboratório** com rede e CPU simuladas (celular: 4G lenta e CPU 4x mais lenta). **INP não existe nesse
  modo**; o **TBT** (tempo de bloqueio) é o substituto usual. Telas logadas usam a sessão da empresa de teste, com cache frio a cada rodada.
- Cada tela é medida 3 vezes e vale a **mediana**; as rodadas individuais ficam no `.json`. Variação de alguns pontos entre execuções é normal.
- As **tarefas cronometradas do Playwright** são a linha de base **automatizada** (sem hesitação nem leitura): servem para comparar versões
  do app e dar o piso de tempo, **não** substituem as tarefas com pessoas (ver `docs/PROTOCOLO_DE_USABILIDADE.md`).
- O banco de métricas acumula empresas de teste de execuções antigas; é inofensivo. Para zerar, apague o banco `LocadoraDeVeiculos_Metricas`.
- **Limite de autenticação por IP:** a API aceita 10 chamadas por minuto por IP em login, cadastro e renovação de token, e a renovação
  roda a cada recarga completa de página. Como o proxy de teste faz tudo parecer um IP só, o Playwright manda o cabeçalho
  `X-Metricas-Ip` (só existe neste proxy) para dar um IP simulado a cada navegação; sem isso, a suíte tomaria `429`.

## Estrutura

```
orquestrar.py            sobe/derruba tudo e roda os scripts Node
apoio/servidor_front.py  build de produção + proxy /api (mesma origem, cookie de sessão funciona)
apoio/semeadura.py       dados de exemplo pela API
apoio/pacote.py          tamanho do pacote
lighthouse/executar.mjs  Lighthouse (puppeteer-core + Edge)
e2e/*                    Playwright (Edge instalado, sem baixar navegador) + axe-core
```
