using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

// Segundo fator por aplicativo autenticador (TOTP) com os provedores do ASP.NET Identity (S18). A chave fica no Identity
// (AspNetUserTokens); o código vale 30 s e os de recuperação valem uma vez.

public class ObterStatusDoisFatoresQueryHandler(UserManager<Usuario> userManager)
    : IRequestHandler<ObterStatusDoisFatoresQuery, Result<StatusDoisFatores>>
{
    public async Task<Result<StatusDoisFatores>> Handle(ObterStatusDoisFatoresQuery query, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(query.UsuarioId.ToString());

        if (usuario is null)
            return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida."));

        var habilitado = await userManager.GetTwoFactorEnabledAsync(usuario);

        return Result.Ok(new StatusDoisFatores(habilitado, habilitado ? await userManager.CountRecoveryCodesAsync(usuario) : 0));
    }
}

public class IniciarDoisFatoresCommandHandler(UserManager<Usuario> userManager)
    : IRequestHandler<IniciarDoisFatoresCommand, Result<ConfiguracaoDoisFatores>>
{
    private const string Emissor = "LocadoraDeVeiculos";

    public async Task<Result<ConfiguracaoDoisFatores>> Handle(IniciarDoisFatoresCommand command, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

        if (usuario?.Email is null)
            return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida."));

        // Trocar a chave de quem já tem 2FA ativo deixaria a conta sem segundo fator sem passar por senha e código.
        if (await userManager.GetTwoFactorEnabledAsync(usuario))
            return Result.Fail(ResultadosErro.EstadoInvalidoErro("O segundo fator já está ativo. Desative-o antes de configurar de novo."));

        await userManager.ResetAuthenticatorKeyAsync(usuario);
        var chave = (await userManager.GetAuthenticatorKeyAsync(usuario))!;

        return Result.Ok(new ConfiguracaoDoisFatores(FormatarChave(chave), MontarUri(usuario.Email, chave)));
    }

    public static string FormatarChave(string chave) =>
        string.Join(' ', Enumerable.Range(0, (chave.Length + 3) / 4).Select(i => chave.Substring(i * 4, Math.Min(4, chave.Length - i * 4)))).ToLowerInvariant();

    public static string MontarUri(string email, string chave) =>
        $"otpauth://totp/{WebUtility.UrlEncode(Emissor)}:{WebUtility.UrlEncode(email)}?secret={chave}&issuer={WebUtility.UrlEncode(Emissor)}&digits=6";
}

public class AtivarDoisFatoresCommandHandler(
    UserManager<Usuario> userManager,
    ILogger<AtivarDoisFatoresCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<AtivarDoisFatoresCommand, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(AtivarDoisFatoresCommand command, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

        if (usuario is null)
            return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida."));

        if (await userManager.GetTwoFactorEnabledAsync(usuario))
            return Result.Fail(ResultadosErro.EstadoInvalidoErro("O segundo fator já está ativo."));

        if (string.IsNullOrEmpty(await userManager.GetAuthenticatorKeyAsync(usuario)))
            return Result.Fail(ResultadosErro.RequisicaoInvalidaErro("Comece a configuração do segundo fator antes de ativá-lo."));

        var codigo = VerificadorDeSegundoFator.Normalizar(command.Codigo);

        if (!await userManager.VerifyTwoFactorTokenAsync(usuario, userManager.Options.Tokens.AuthenticatorTokenProvider, codigo))
            return Result.Fail(ResultadosErro.RequisicaoInvalidaErro("Código incorreto. Confira o aplicativo e tente de novo."));

        await userManager.SetTwoFactorEnabledAsync(usuario, true);
        var codigos = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 10))?.ToList() ?? [];

        logger.LogInformation("Segundo fator ativado pelo usuário {UserId}.", usuario.Id);

        if (auditoria is not null)
            await auditoria.RegistrarAsync(EventosDeAutenticacao.DoisFatoresAtivado, usuario, cancellationToken);

        // Os códigos de recuperação só aparecem agora; o servidor guarda apenas o necessário para conferi-los.
        return Result.Ok<IReadOnlyList<string>>(codigos);
    }
}

public class DesativarDoisFatoresCommandHandler(
    UserManager<Usuario> userManager,
    ILogger<DesativarDoisFatoresCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<DesativarDoisFatoresCommand, Result>
{
    private const string Recusado = "Senha ou código incorretos.";

    public async Task<Result> Handle(DesativarDoisFatoresCommand command, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

        if (usuario is null)
            return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida."));

        if (await userManager.IsLockedOutAsync(usuario))
            return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(Recusado));

        // Desligar a proteção exige as duas coisas: um token roubado não a desliga.
        var verificador = new VerificadorDeSegundoFator(userManager);
        var senhaOk = await userManager.CheckPasswordAsync(usuario, command.Senha ?? string.Empty);
        var segundo = senhaOk ? await verificador.VerificarAsync(usuario, command.Codigo) : ResultadoDoSegundoFator.CodigoInvalido;

        if (!senhaOk)
            await userManager.AccessFailedAsync(usuario);

        if (!senhaOk || segundo != ResultadoDoSegundoFator.Liberado)
            return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(Recusado));

        await userManager.SetTwoFactorEnabledAsync(usuario, false);
        await userManager.ResetAuthenticatorKeyAsync(usuario);
        await userManager.ResetAccessFailedCountAsync(usuario);

        logger.LogInformation("Segundo fator desativado pelo usuário {UserId}.", usuario.Id);

        if (auditoria is not null)
            await auditoria.RegistrarAsync(EventosDeAutenticacao.DoisFatoresDesativado, usuario, cancellationToken);

        return Result.Ok();
    }
}
