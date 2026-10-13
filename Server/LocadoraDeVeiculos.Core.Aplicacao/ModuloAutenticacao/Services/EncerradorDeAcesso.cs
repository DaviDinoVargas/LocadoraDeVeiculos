using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

public interface IEncerradorDeAcesso
{
    Task EncerrarAsync(Usuario usuario, string motivo, CancellationToken cancellationToken);
}

/// <summary>
/// Encerra de verdade o acesso de uma conta: derruba as sessões (refresh tokens e versão do access token), bloqueia a
/// conta para sempre (lockout até 9999: nem o login nem a recuperação de senha a reativam) e apaga a biometria e o
/// consentimento, que não têm mais finalidade. Usado ao desligar um funcionário (S26) e ao encerrar uma conta (S1).
/// </summary>
public class EncerradorDeAcesso(
    RefreshTokenProvider refreshTokenProvider,
    UserManager<Usuario> userManager,
    IFacialAuthClient facialAuthClient
) : IEncerradorDeAcesso
{
    public static bool ContaEncerrada(Usuario usuario) => usuario.LockoutEnd == DateTimeOffset.MaxValue;

    public async Task EncerrarAsync(Usuario usuario, string motivo, CancellationToken cancellationToken)
    {
        await refreshTokenProvider.RevogarTokensUsuarioAsync(usuario.Id, motivo);

        usuario.AccessTokenVersionId = Guid.NewGuid();
        await userManager.UpdateAsync(usuario);

        await userManager.SetLockoutEnabledAsync(usuario, true);
        await userManager.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);

        if (!string.IsNullOrWhiteSpace(usuario.Email))
            await facialAuthClient.RemoverAsync(FacialAuthClient.PersonIdParaEmail(usuario.Email), cancellationToken);
    }
}
