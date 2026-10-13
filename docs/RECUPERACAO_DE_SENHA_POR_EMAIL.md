# Recuperação de senha por e-mail

O botão "Esqueci minha senha" só **envia e-mail de verdade** quando o servidor SMTP está configurado. Sem isso:

| Situação | O que acontece |
|---|---|
| `ASPNETCORE_ENVIRONMENT=Development` e **sem** `SMTP_HOST` | o e-mail **não sai**: o texto, com o link, é escrito no **log da API** (procure por `[E-MAIL DE DESENVOLVIMENTO — SMTP não configurado]`) |
| Fora de Development e sem `SMTP_HOST` | nada é enviado; a API registra um aviso pedindo para configurar o SMTP |
| Com `SMTP_HOST` | o e-mail é enviado pelo SMTP |

A tela responde sempre a mesma mensagem ("Se existir uma conta com este e-mail…"), exista a conta ou não, e o pedido é processado em segundo plano. Por isso **a tela nunca avisa que o envio falhou**: confira o log da API.

## Como configurar (segredos nas user-secrets, nunca no repositório)

```bash
dotnet user-secrets set SMTP_HOST smtp.gmail.com --project Server/web-api
dotnet user-secrets set SMTP_PORT 587 --project Server/web-api
dotnet user-secrets set SMTP_USER seu.email@gmail.com --project Server/web-api
dotnet user-secrets set SMTP_PASSWORD <senha-de-app> --project Server/web-api
dotnet user-secrets set SMTP_FROM seu.email@gmail.com --project Server/web-api
```

- `SMTP_SSL` (padrão `true`, STARTTLS na porta 587). Use `false` só em servidor local de teste.
- No Gmail é preciso ter a verificação em duas etapas e criar uma **senha de app** (a senha normal da conta é recusada). Outras opções: Outlook/Office 365 (`smtp.office365.com`, 587), Brevo, Mailtrap (caixa de testes que não entrega a terceiros).
- `FRONTEND_URL` define o endereço do link enviado (padrão `http://localhost:4200`).
- O e-mail pode cair na caixa de spam; o link vale por 24 horas e só pode ser usado uma vez.

## Como foi validado

O teste `RecuperacaoDeSenhaPorSmtpTests` (Web API) sobe um servidor SMTP em memória, pede a recuperação, confere remetente, destinatário e link recebidos, redefine a senha com o link, confirma que a senha antiga deixa de valer e que o link não funciona uma segunda vez. Também confere que um e-mail sem conta não recebe nada.
