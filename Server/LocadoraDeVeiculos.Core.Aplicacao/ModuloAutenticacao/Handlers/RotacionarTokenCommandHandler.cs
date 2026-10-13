using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

public class RotacionarTokenCommandHandler(
    LocadoraDeVeiculosDbContext dbContext,
    AccessTokenProvider accessTokenProvider,
    RefreshTokenProvider refreshTokenProvider,
    ILogger<RotacionarTokenCommandHandler> logger,
    UserManager<Usuario>? userManager = null,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<RotacionarTokenCommand, Result<(AccessToken, RefreshToken, string)>>
{
    public async Task<Result<(AccessToken, RefreshToken, string)>> Handle(RotacionarTokenCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var (usuarioEncontrado, novoRefreshToken, novoTokenBruto) = await refreshTokenProvider.RotacionarRefreshTokenAsync(command.RefreshTokenString);

            usuarioEncontrado.AccessTokenVersionId = Guid.NewGuid();

            var novoAccessToken = await accessTokenProvider.GerarAccessTokenAsync(usuarioEncontrado);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok((novoAccessToken, novoRefreshToken, novoTokenBruto));
        }
        catch (ReusoDeRefreshTokenException ex)
        {
            // Token já trocado que volta: cookie copiado. As sessões já foram derrubadas; o dono da empresa precisa ficar sabendo.
            logger.LogWarning("Rotação de token recusada: reuso de refresh token do usuário {UserId}.", ex.UsuarioId);

            var dono = userManager is null ? null : await userManager.FindByIdAsync(ex.UsuarioId.ToString());
            if (dono is not null && auditoria is not null)
                await auditoria.RegistrarAsync(EventosDeAutenticacao.ReusoDeRefreshToken, dono, cancellationToken);

            return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida ou expirada. Faça login novamente."));
        }
        catch (SecurityTokenException ex)
        {
            // Refresh token inexistente, expirado ou já usado/revogado: é sessão inválida (401),
            // não erro do servidor. O front trata o 401 mandando o usuário para o login.
            logger.LogWarning("Rotação de token recusada: {Motivo}", ex.Message);

            return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida ou expirada. Faça login novamente."));
        }
        catch (Exception ex)
        {
            // Não logar o comando: ele carrega o refresh token em texto.
            logger.LogError(ex, "Ocorreu um erro durante a rotação de token.");

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
