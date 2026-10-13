# Testes de segurança (ataque e carga)

Scripts que **atacam e sobrecarregam uma instância de teste** da aplicação e registram o que aconteceu.
Só a biblioteca padrão do Python é usada (nenhuma dependência para instalar).

## Regras que não mudam

- **Só contra o seu ambiente local.** Nunca aponte estes scripts para um servidor que não seja seu.
- **Banco separado:** a API de teste usa `LocadoraDeVeiculos_Seguranca`; o script recusa rodar se não conseguir garantir isso.
  A cadeia de conexão vem das `user-secrets` e nunca é impressa.
- **Resultados ficam FORA do git** (descrevem falhas abertas): por padrão em `../Seguranca-Resultados` (ou o que estiver em `SEGURANCA_RESULTADOS`).
  O script recusa gravar dentro do repositório; o `.gitignore` e o hook de pré-commit também barram.
- **Sem segredos no código:** contas e senhas de teste são geradas a cada execução. Ao final, o teste confere se alguma delas apareceu nos logs.
- A webcam real nunca é ligada: os ataques ao ML usam um índice de câmera inexistente.

## Como rodar

```bash
./Testes-Seguranca/rodar.sh ataques            # ~2 min: sobe API + ML de teste e ataca
./Testes-Seguranca/rodar.sh carga              # ~3 min: 10/50/100 usuários simultâneos
./Testes-Seguranca/rodar.sh tudo               # os dois
./Testes-Seguranca/rodar.sh comparar           # última execução x anterior
./Testes-Seguranca/rodar.sh limpar             # apaga o banco de teste
```

Opções: `--sem-ml` (não sobe o ML), `--sem-build` (não recompila a API). **Pare a Web API de uso antes** (o build troca os binários).
Portas usadas: 5180 (API de ataque), 5181 (API de carga, com limite global alto), 8001 (ML em modo produção).

## O que é testado

| Arquivo | Assunto |
|---|---|
| `ataques/a_autenticacao.py` | JWT forjado (payload, `alg=none`, outra chave), logout, reuso de refresh token, força bruta e bloqueio, enumeração de usuários (mensagem e tempo), rate limit de login/cadastro/global |
| `ataques/b_autorizacao.py` | papéis (anônimo/Funcionário/Empresa), isolamento entre empresas por ID (IDOR), mass assignment, funcionário desligado, auditoria, troca de senha |
| `ataques/c_entradas.py` | SQL injection (inclusive por tempo), XSS armazenado, corpo gigante/malformado, CPF inválido, foto do veículo |
| `ataques/d_configuracao.py` | cabeçalhos de segurança, CORS, anti-CSRF, vazamento em erros, verificações estáticas do código (ML, pacotes mortos) |
| `ataques/e_ml.py` | token interno, JWT nas câmeras, isolamento por empresa no ML, `/docs`/`/health` em produção, limites |
| `ataques/f_conta_e_privacidade.py` | consentimento do rosto, 2FA de ponta a ponta (TOTP e códigos de recuperação), recuperação de senha sem enumeração, encerrar conta, trilha sem segredos |
| `carga/cenarios.py` | leituras em 10/50/100 usuários, login (hash caro), login com senha errada; p50/p95/p99, req/s, 4xx/429/5xx |

## Como ler o resultado

Cada verificação tem **ID, severidade, item do relatório (S#), esperado e obtido**. *Reprovada* significa **achado**: o ataque funcionou
ou a proteção não existe. *Aprovada* significa que a defesa segurou. Uma verificação só vale se ela **consegue falhar**: ao criar uma nova,
confira que ela reprova quando a proteção é removida.

Cada execução gera uma pasta `AAAA-MM-DD_HHMMSS_<commit>/` com `resultado.json`, `resumo.md` e os logs das instâncias.
Uma correção só conta como fechada quando o ataque que a encontrou passa a ser **aprovado** em uma nova execução (`rodar.sh comparar`).

## Como acrescentar um ataque

Crie uma função em um dos módulos de `ataques/` (ou um módulo novo e inclua-o em `MODULOS_ATAQUE` de `executar.py`) e chame
`ctx.reg.checar(id, categoria, titulo, esperado, obtido, passou, severidade, ref)`. Use `ip_unico()` em cada requisição para que
um teste não consuma o rate limit do outro, e `ctx.lembrar(...)` em toda senha/token criado, para a checagem de vazamento nos logs.
