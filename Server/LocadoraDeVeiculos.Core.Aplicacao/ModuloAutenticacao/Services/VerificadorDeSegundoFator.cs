using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

public enum ResultadoDoSegundoFator
{
    /// <summary>A conta não usa 2FA, ou o código estava certo.</summary>
    Liberado,
    /// <summary>A conta usa 2FA e nenhum código foi informado.</summary>
    CodigoObrigatorio,
    /// <summary>O código estava errado (conta como falha no bloqueio de conta).</summary>
    CodigoInvalido
}

/// <summary>
/// Segundo fator por aplicativo autenticador (TOTP, RFC 6238) ou código de recuperação de uso único (S18). Chamado depois
/// que a senha (ou o rosto) já passou. Errar o código soma no bloqueio de conta: com 5 erros a conta trava, então não dá para
/// adivinhar um código de 6 dígitos.
/// </summary>
public class VerificadorDeSegundoFator(UserManager<Usuario> userManager)
{
    public async Task<ResultadoDoSegundoFator> VerificarAsync(Usuario usuario, string? codigo)
    {
        if (!await userManager.GetTwoFactorEnabledAsync(usuario))
            return ResultadoDoSegundoFator.Liberado;

        var limpo = Normalizar(codigo);

        if (limpo.Length == 0)
            return ResultadoDoSegundoFator.CodigoObrigatorio;

        if (await userManager.VerifyTwoFactorTokenAsync(usuario, userManager.Options.Tokens.AuthenticatorTokenProvider, limpo))
            return ResultadoDoSegundoFator.Liberado;

        // Código de recuperação (formato XXXXX-XXXXX do Identity): vale uma vez só.
        var recuperacao = (codigo ?? string.Empty).Trim();
        var resgate = await userManager.RedeemTwoFactorRecoveryCodeAsync(usuario, recuperacao);

        if (resgate.Succeeded)
            return ResultadoDoSegundoFator.Liberado;

        await userManager.AccessFailedAsync(usuario);

        return ResultadoDoSegundoFator.CodigoInvalido;
    }

    /// <summary>Aplicativos mostram "123 456" ou "123-456": tira espaços e hífens para conferir só os dígitos.</summary>
    public static string Normalizar(string? codigo) =>
        (codigo ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim();
}
