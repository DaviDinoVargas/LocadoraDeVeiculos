# Protocolo de usabilidade (SUS, tarefas cronometradas e heurísticas de Nielsen)

Este documento é o **roteiro para coletar com pessoas** as métricas de usabilidade do TCC. As métricas automáticas (Lighthouse,
axe-core, tempos de tarefa no Playwright, tamanho do pacote, tempos da API) estão em `Testes-Metricas/` e **não substituem** esta
coleta: máquina não hesita, não lê e não se perde. Aqui o que importa é a experiência de quem usa.

> Estado: **protocolo pronto, coleta com pessoas ainda não feita.** As tabelas abaixo são modelos para preencher; nada aqui é resultado.

## 1. Quem e como

- **Participantes:** 5 a 8 pessoas que representem quem usaria o sistema (atendente de locadora, gestor). Cinco já revelam a maior
  parte dos problemas de uso numa rodada de teste (Nielsen); mais pessoas dão mais segurança nos números.
- **Ambiente:** o sistema rodando com os dados de exemplo (`python Testes-Metricas/orquestrar.py manter` sobe API, front de produção e dados
  de teste; use uma empresa nova por participante). Tela única, mouse e teclado, sem ajuda do avaliador durante as tarefas.
- **Consentimento:** explicar que se testa o sistema, não a pessoa; pode parar a qualquer momento; não coletar dados pessoais reais
  (usar só os dados de exemplo). Registrar apenas um código (P1, P2...), idade em faixa e experiência com sistemas de gestão.
- **Ordem:** (1) apresentação de 2 minutos, (2) as 3 tarefas abaixo, (3) o questionário SUS, (4) 3 perguntas abertas.

## 2. Tarefas cronometradas

O avaliador lê a tarefa, **não explica a tela** e cronometra do primeiro clique até o resultado visível. Registrar tempo, erros e se concluiu
sozinho. "Erro" é qualquer ação que o participante precisou desfazer ou repetir, ou clique em lugar errado; "ajuda" é qualquer dica do avaliador (conta como falha).

| # | Tarefa dada ao participante | Concluída quando |
|---|---|---|
| T1 | "Cadastre uma nova cliente pessoa física: Marina Lopes, CPF e telefone à sua escolha." | O cliente aparece na lista de clientes. |
| T2 | "Reserve um carro para a cliente Ana Souza, saindo daqui a 40 dias e voltando 3 dias depois." | O aluguel aparece na lista como Reservado. |
| T3 | "Registre a devolução do aluguel em andamento que está com a cliente Bruno Lima." | A devolução aparece na lista de devoluções, com condutor e cliente. |

### Planilha de registro (uma linha por participante e tarefa)

| Participante | Tarefa | Tempo (s) | Erros | Pediu ajuda? | Concluiu sozinho? | Observação |
|---|---|---|---|---|---|---|
| P1 | T1 | | | | | |

### Como calcular

- **Taxa de sucesso** = tarefas concluídas sem ajuda ÷ tarefas tentadas, por tarefa e geral.
- **Tempo por tarefa** = mediana dos tempos das tarefas concluídas (a média engana com poucas pessoas); informar também o mínimo e o máximo.
- **Taxa de erro** = total de erros ÷ total de tarefas tentadas (erros por tarefa).
- **Comparação:** os tempos do Playwright (`e2e/resumo.md`) são o **piso** (execução sem hesitação); a razão tempo humano ÷ piso mostra onde a interface faz a pessoa parar.

## 3. Questionário SUS (System Usability Scale)

Aplicar logo depois das tarefas. Cada afirmação é respondida de **1 (discordo totalmente) a 5 (concordo totalmente)**.

1. Eu acho que gostaria de usar este sistema com frequência.
2. Eu achei o sistema desnecessariamente complexo.
3. Eu achei o sistema fácil de usar.
4. Eu acho que precisaria de ajuda de uma pessoa com conhecimentos técnicos para usar o sistema.
5. Eu achei as várias funções do sistema muito bem integradas.
6. Eu acho que o sistema apresenta muita inconsistência.
7. Eu imagino que as pessoas aprenderiam a usar este sistema rapidamente.
8. Eu achei o sistema muito complicado de usar.
9. Eu me senti confiante ao usar o sistema.
10. Eu precisei aprender várias coisas novas antes de conseguir usar o sistema.

**Cálculo (por participante):** nas afirmações **ímpares** (1, 3, 5, 7, 9) some `resposta − 1`; nas **pares** (2, 4, 6, 8, 10) some `5 − resposta`;
some tudo e **multiplique por 2,5**. O resultado vai de 0 a 100 (não é percentual). O SUS do sistema é a média dos participantes.

Referência de leitura: a média histórica do SUS é de cerca de **68**; acima disso o sistema é considerado acima da média, e acima de ~80 é muito bom.
Com poucos participantes, apresentar a média **e** a dispersão (mínimo e máximo), não só um número.

### Perguntas abertas

1. O que foi mais difícil ou confuso?
2. O que você mais gostou?
3. O que mudaria primeiro?

## 4. Checklist das heurísticas de Nielsen

Avaliação com 2 ou mais avaliadores (de preferência com quem não programou), cada um anota **problemas de 0 a 4** (0 = não é problema, 4 = catastrófico) por heurística.
A coluna "Evidência no sistema" mostra o que já existe (conferido no código e, na maior parte, coberto por teste automatizado); ela ajuda o avaliador, **não** dá a nota.

| # | Heurística | Evidência no sistema (verificada) | Nota dos avaliadores (0–4) |
|---|---|---|---|
| 1 | Visibilidade do estado do sistema | Esqueletos de carregamento com `aria-busy`, botão "Salvando…", aviso "Alterações não salvas", avisos de sucesso e erro | |
| 2 | Correspondência com o mundo real | Interface em português com termos do negócio (aluguel, devolução, condutor, caução); placa no formato `ABC-1D23` em todas as telas | |
| 3 | Controle e liberdade do usuário | "Cancelar" em todo formulário; diálogo "Descartar alterações?" ao sair com dados não salvos; Esc fecha diálogos | |
| 4 | Consistência e padrões | Mesmos componentes em todas as telas (tabela, formulário, selos de status, barra de ações fixa no rodapé) | |
| 5 | Prevenção de erros | Máscaras (CPF, CNPJ, telefone, placa), validação de CPF/CNPJ, confirmação antes de excluir, veículos filtrados pelo período | |
| 6 | Reconhecimento em vez de memorização | Rótulos sempre visíveis, paleta de comandos (Ctrl K), filtros mostrados como chips, estado da tabela na URL | |
| 7 | Flexibilidade e eficiência de uso | Atalho Ctrl K, preferências de colunas e densidade da tabela, exportação em CSV e PDF | |
| 8 | Estética e design minimalista | Tokens de cor e tipografia únicos (Material 3), temas claro e escuro, contraste verificado pelo axe-core | |
| 9 | Ajudar a reconhecer, diagnosticar e corrigir erros | Mensagem do servidor no próprio campo, com foco; texto de erro específico com ícone | |
| 10 | Ajuda e documentação | **Lacuna conhecida:** não há ajuda contextual nem manual dentro do sistema | |

## 5. O que entregar ao final

Uma tabela por métrica (SUS médio e dispersão, taxa de sucesso, mediana de tempo e taxa de erro por tarefa, problemas por heurística) e a lista dos
5 maiores problemas observados, cada um com a tela, quantos participantes tropeçaram e a correção sugerida. Guardar as planilhas **fora do repositório**
(dados de participantes), junto com os resultados de `Testes-Metricas`.
