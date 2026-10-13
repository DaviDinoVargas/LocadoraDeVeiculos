using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

public interface IEnviadorDeEmail
{
    /// <summary>Envia um e-mail de texto simples. Pode lançar: quem chama (a fila) trata e registra.</summary>
    Task EnviarAsync(string para, string assunto, string corpo, CancellationToken cancellationToken);
}

/// <summary>
/// SMTP real. Configuração: SMTP_HOST, SMTP_PORT (587), SMTP_USER, SMTP_PASSWORD (segredo: user-secrets/variável de
/// ambiente, nunca no repositório), SMTP_FROM e SMTP_SSL (true). A senha nunca é registrada em log.
/// </summary>
public sealed class EnviadorDeEmailSmtp(IConfiguration configuration, ILogger<EnviadorDeEmailSmtp> logger) : IEnviadorDeEmail
{
    public async Task EnviarAsync(string para, string assunto, string corpo, CancellationToken cancellationToken)
    {
        var host = configuration["SMTP_HOST"]!;
        var porta = int.TryParse(configuration["SMTP_PORT"], out var p) ? p : 587;
        var de = configuration["SMTP_FROM"] ?? configuration["SMTP_USER"] ?? "nao-responda@locadora.local";
        var ssl = !string.Equals(configuration["SMTP_SSL"], "false", StringComparison.OrdinalIgnoreCase);

        using var cliente = new SmtpClient(host, porta) { EnableSsl = ssl, Timeout = 15_000 };

        if (!string.IsNullOrWhiteSpace(configuration["SMTP_USER"]))
            cliente.Credentials = new NetworkCredential(configuration["SMTP_USER"], configuration["SMTP_PASSWORD"]);

        using var mensagem = new MailMessage(de, para, assunto, corpo);

        await cliente.SendMailAsync(mensagem, cancellationToken);

        logger.LogInformation("E-mail \"{Assunto}\" enviado.", assunto);
    }
}

/// <summary>
/// Só em Development e sem SMTP configurado: em vez de enviar, mostra o e-mail no log, para poder testar a recuperação de
/// senha localmente. NUNCA usado fora de desenvolvimento (o link carrega um token de redefinição).
/// </summary>
public sealed class EnviadorDeEmailDeDesenvolvimento(ILogger<EnviadorDeEmailDeDesenvolvimento> logger) : IEnviadorDeEmail
{
    public Task EnviarAsync(string para, string assunto, string corpo, CancellationToken cancellationToken)
    {
        logger.LogWarning("[E-MAIL DE DESENVOLVIMENTO — SMTP não configurado] Para: {Para} | Assunto: {Assunto}\n{Corpo}", para, assunto, corpo);

        return Task.CompletedTask;
    }
}

/// <summary>Fora de desenvolvimento e sem SMTP: não envia nada e avisa. A recuperação de senha fica indisponível, sem vazar o motivo ao cliente.</summary>
public sealed class EnviadorDeEmailIndisponivel(ILogger<EnviadorDeEmailIndisponivel> logger) : IEnviadorDeEmail
{
    public Task EnviarAsync(string para, string assunto, string corpo, CancellationToken cancellationToken)
    {
        logger.LogWarning("Não foi possível enviar o e-mail \"{Assunto}\": configure SMTP_HOST (e SMTP_FROM/SMTP_USER/SMTP_PASSWORD).", assunto);

        return Task.CompletedTask;
    }
}
