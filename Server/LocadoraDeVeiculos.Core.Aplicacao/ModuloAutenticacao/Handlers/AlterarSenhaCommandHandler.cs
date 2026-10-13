using FluentResults;
using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

/// <summary>
/// Troca de senha autenticada (S27). Exige a senha atual, conta o erro no bloqueio de conta (um token roubado não
/// serve para descobrir a senha por força bruta) e, ao terminar, derruba todas as sessões, inclusive a atual: o cliente
/// precisa entrar de novo com a senha nova.
/// </summary>
public class AlterarSenhaCommandHandler(
    UserManager<Usuario> userManager,
    RefreshTokenProvider refreshTokenProvider,
    IValidator<AlterarSenhaCommand> validator,
    ILogger<AlterarSenhaCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<AlterarSenhaCommand, Result>
{
    private const string SenhaAtualIncorreta = "A senha atual está incorreta.";

    public async Task<Result> Handle(AlterarSenhaCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var validacao = await validator.ValidateAsync(command, cancellationToken);

            if (!validacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(validacao.Errors.Select(e => e.ErrorMessage)));

            var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

            if (usuario is null)
                return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida."));

            if (await userManager.IsLockedOutAsync(usuario))
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(SenhaAtualIncorreta));

            if (!await userManager.CheckPasswordAsync(usuario, command.SenhaAtual))
            {
                await userManager.AccessFailedAsync(usuario);
                logger.LogWarning("Troca de senha recusada (senha atual incorreta) para o usuário {UserId}.", usuario.Id);

                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(SenhaAtualIncorreta));
            }

            var troca = await userManager.ChangePasswordAsync(usuario, command.SenhaAtual, command.NovaSenha);

            if (!troca.Succeeded)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(troca.Errors.Select(e => e.Description)));

            await userManager.ResetAccessFailedCountAsync(usuario);

            await EncerrarSessoesAsync(usuario);

            logger.LogInformation("Senha alterada pelo usuário {UserId}; sessões encerradas.", usuario.Id);

            if (auditoria is not null)
                await auditoria.RegistrarAsync(EventosDeAutenticacao.SenhaAlterada, usuario, cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao alterar a senha do usuário {UserId}.", command.UsuarioId);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }

    private async Task EncerrarSessoesAsync(Usuario usuario)
    {
        usuario.AccessTokenVersionId = Guid.NewGuid();
        await userManager.UpdateAsync(usuario);
        await refreshTokenProvider.RevogarTokensUsuarioAsync(usuario.Id, "Troca de senha");
    }
}
