using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

public class SairCommandHandler(
    LocadoraDeVeiculosDbContext dbContext,
    UserManager<Usuario> userManager,
    RefreshTokenProvider refreshTokenProvider,
    ILogger<SairCommandHandler> logger
) : IRequestHandler<SairCommand, Result>
{
    public async Task<Result> Handle(SairCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var token = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == command.RefreshTokenHash, cancellationToken);

            if (token is null)
                throw new SecurityTokenException("O token de rotação não foi encontrado.");

            var usuarioId = token.UsuarioId;

            await refreshTokenProvider.RevogarTokensUsuarioAsync(usuarioId, "Logout");

            var usuarioEncontrado = await userManager.FindByIdAsync(usuarioId.ToString());

            if (usuarioEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro("Não foi possível encontrar o usuário requisitado."));

            usuarioEncontrado.AccessTokenVersionId = Guid.NewGuid();

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ocorreu um erro durante o logout {@Command}.",
                command
            );

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}