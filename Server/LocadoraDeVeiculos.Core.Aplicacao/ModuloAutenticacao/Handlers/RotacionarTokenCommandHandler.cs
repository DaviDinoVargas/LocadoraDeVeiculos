using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

public class RotacionarTokenCommandHandler(
    LocadoraDeVeiculosDbContext dbContext,
    AccessTokenProvider accessTokenProvider,
    RefreshTokenProvider refreshTokenProvider,
    ILogger<RotacionarTokenCommandHandler> logger
) : IRequestHandler<RotacionarTokenCommand, Result<(AccessToken, RefreshToken)>>
{
    public async Task<Result<(AccessToken, RefreshToken)>> Handle(RotacionarTokenCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var (usuarioEncontrado, novoRefreshToken) = await refreshTokenProvider.RotacionarRefreshTokenAsync(command.RefreshTokenString);

            usuarioEncontrado.AccessTokenVersionId = Guid.NewGuid();

            var novoAccessToken = await accessTokenProvider.GerarAccessTokenAsync(usuarioEncontrado);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok((novoAccessToken, novoRefreshToken));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ocorreu um erro durante a rotação de token {@Command}.",
                command
            );

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}