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

Opções: `--sem-ml` (não sobe o ML), `--sem-build` (não recompila a API). A API de teste é compilada em `../Seguranca-Build` (fora do repositório), então **não precisa parar** a Web API que você deixou rodando.
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
| `ataques/g_robustez.py` | JWT com claims inválidas, vazamento de campos, fuzz de IDs/JSON/cabeçalhos, senhas gigantes, força bruta e replay do 2FA, CORS, mass assignment, reset de senha, Host header |
| `ataques/h_negocio.py` | reserva dupla do mesmo veículo, IDs de outra empresa no corpo, períodos absurdos, preço enviado pelo cliente, cupom de uso único e transições de estado em corrida |
| `ataques/j_concorrencia.py` | **corridas**: 10 cadastros simultâneos com o mesmo CPF/CNPJ/CNH/placa/nome/código/e-mail (cliente, condutor, carro, grupo, cupom, taxa, parceiro, funcionário, conta), 6 devoluções do mesmo aluguel, iniciar × cancelar, excluir cliente × criar aluguel; cada verificação confere o banco |
| `ataques/k_ml_fuzz.py` | **fuzz do ML**: imagens corrompidas e bomba de descompressão em `/face` (mede o pico de memória do ML), corpos mal formados e de 30 MB, texto adversário e JSON de tipo errado em `/chat`, câmera e fps fora do esperado (só índices inexistentes; a webcam nunca liga) |
| `ataques/l_ssrf.py` | **SSRF com canário**: um servidor local que só conta conexões é plantado como URL em todos os campos de texto (foto do veículo, descrições, nomes, endereços, conta, chat); as telas que os mostram são consultadas; zero conexões |
| `ataques/m_sessoes_e_https.py` | **sessões simultâneas** (12 logins; sair, reuso de refresh token e troca de senha encerram todas as sessões) e **API em modo produção com HTTPS de verdade** (Kestrel + certificado de desenvolvimento): redirecionamento http→https, HSTS, cookie Secure/HttpOnly/SameSite/Path, sem Swagger, erros sem stack trace, TLS 1.2+ |
| `ataques/n_duas_instancias.py` | **duas instâncias da API no mesmo banco**: reserva do mesmo carro (12 rodadas), cupom de uso único, devolução repetida, funcionário com o mesmo CPF e iniciar × cancelar com os pedidos divididos entre as instâncias |
| `zap_dast.py` (`rodar.sh zap`) | **OWASP ZAP**: varredura ativa autenticada guiada pelo Swagger da API (ver seção abaixo) |
| `carga/cenarios.py` | leituras em 10/50/100 usuários, login (hash caro), login com senha errada; p50/p95/p99, req/s, 4xx/429/5xx |

## OWASP ZAP (`./rodar.sh zap`)

Varredura **ativa** (o ZAP ataca de verdade: injeção, XSS, travessia de caminho, etc.) contra a API de teste, autenticada como uma empresa que tem dados.
Roda em ~30 min. O ZAP e o Java ficam FORA do repositório; instalação única:

1. Instalador oficial do ZAP (`ZAP_2_17_0_windows.exe`, github.com/zaproxy/zaproxy/releases) instalado em `A:erramentas-seg\zap` (`-q -dir ...`; confira o SHA-256 publicado na página da versão).
2. Java 17 (Eclipse Temurin JRE, zip da Adoptium) descompactado em `A:erramentas-seg\jre\jdk-17...-jre`. O instalador do ZAP não traz Java: use `JAVA_HOME` apontando para ele ao instalar.
3. Variáveis opcionais: `ZAP_HOME` (pasta do ZAP) e `JRE_HOME_ZAP` (pasta que contém `jdk-*-jre`).

O que o script faz: sobe a API de teste com limites altos (o scanner não pode ser barrado pelo rate limit), cria a empresa com dados, sobe o ZAP em modo daemon
(chave da API gerada na hora), importa o `/swagger/v1/swagger.json`, injeta o token JWT em toda requisição (renovado a cada 8 min; o token vale 15) e varre.
Reprova se houver alerta de risco **alto** ou **médio** que não esteja em `ACEITOS` (cada item aceito tem o motivo escrito no script) e se a varredura
não for de verdade (poucas URLs, poucas requisições, poucos endpoints com resposta 2xx).

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
