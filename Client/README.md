# Client — front-end da Locadora de Veículos

Aplicação Angular 21 (componentes standalone, signals, `OnPush`) com Tailwind CSS 4 e componentes
[Spartan UI](https://www.spartan.ng) (`src/app/shared/ui`, já ajustados ao tema do projeto). Fala com a Web API
(.NET) e com o serviço de Machine Learning (FastAPI) pelo proxy de desenvolvimento.

Requisitos: Node 20.19 ou superior. Para as telas funcionarem, a Web API (.NET) e o serviço de ML precisam estar no ar.

## Rodar

```bash
npm install
npm start          # ng serve em http://localhost:4200
```

O `proxy.conf.json` encaminha `/api/**` para a Web API (`https://localhost:7064`) e `/api/cameras`,
`/api/ml/chat` e `/stream` para o serviço de ML (`http://localhost:8000`). Da raiz do repositório,
`npm start` sobe este front e o serviço de ML juntos; a Web API é iniciada à parte (`dotnet run --project Server/web-api`).

## Comandos

| Comando | O que faz |
|---|---|
| `npm start` | servidor de desenvolvimento com recarga automática |
| `npm run build` | build de produção em `dist/client-app` |
| `npm run watch` | build de desenvolvimento que recompila ao salvar |
| `npm test` | specs com Karma e Jasmine (`ng test`) |

## Estrutura de `src/app`

- `auth/` — login, cadastro, recuperação de senha, 2FA, guardas e interceptadores.
- `core/` — serviços e regras sem tela: erros da API, confirmação, navegação, permissões por cargo, tema, ícones.
- `shared/layout` — casca do app (barra lateral, cabeçalho, paleta de comandos) e blocos de página.
- `shared/form-fields` — campos, barra de ações e `crudForm` (cadastro e edição com um único componente).
- `shared/data-table` — tabela com busca, filtros, colunas e densidade; `crudLista` cuida de listar e excluir.
- `shared/charts`, `shared/mascaras`, `shared/validators`, `shared/ui` — gráficos, máscaras (CPF, CNPJ, telefone,
  placa), validadores e os componentes base do Spartan.
- `Components/` — uma pasta por módulo de negócio (clientes, veículos, aluguéis, devoluções, monitoramento...).

## Componentes do Spartan

Os componentes de `shared/ui` foram gerados pelo gerador do Spartan e depois personalizados (botão em pílula,
tabela, diálogos, tema). O gerador (`@spartan-ng/cli`) não é dependência do projeto: para criar um componente novo,
rode-o sob demanda com `npx` e confira o resultado antes de commitar, porque ele reescreve `tsconfig.json`.
