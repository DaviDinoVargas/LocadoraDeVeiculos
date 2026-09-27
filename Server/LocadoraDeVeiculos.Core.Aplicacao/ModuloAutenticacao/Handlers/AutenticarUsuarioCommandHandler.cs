using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

public class AutenticarUsuarioCommandHandler(
    UserManager<Usuario> userManager,
    SignInManager<Usuario> signInManager,
    AccessTokenProvider tokenProvider,
    RefreshTokenProvider refreshTokenProvider,
    ILogger<AutenticarUsuarioCommandHandler> logger
) : IRequestHandler<AutenticarUsuarioCommand, Result<(AccessToken, RefreshToken, string)>>
{
    private const string MensagemGenericaDeFalha = "Usuário ou senha inválidos.";

    public async Task<Result<(AccessToken, RefreshToken, string)>> Handle(
        AutenticarUsuarioCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var usuarioEncontrado = await userManager.FindByEmailAsync(command.Email);

            // Mensagem idêntica para "usuário não existe" e "senha incorreta": evita enumeração de e-mails.
            if (usuarioEncontrado is null)
            {
                logger.LogWarning("Tentativa de login com e-mail não cadastrado: {Email}.", command.Email);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            // SignInManager (em vez de UserManager.CheckPasswordAsync direto) é o que aciona o
            // lockout do ASP.NET Identity: incrementa AccessFailedCount e bloqueia a conta após
            // muitas tentativas erradas, configurado em IdentifyConfig (options.Lockout).
            var resultadoLogin = await signInManager.CheckPasswordSignInAsync(
                usuarioEncontrado,
                command.Senha,
                lockoutOnFailure: true
            );

            if (resultadoLogin.IsLockedOut)
            {
                logger.LogWarning("Login bloqueado por excesso de tentativas para o usuário {UserId}.", usuarioEncontrado.Id);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                    "Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde."));
            }

            if (!resultadoLogin.Succeeded)
            {
                logger.LogWarning("Tentativa de login com senha incorreta para o usuário {UserId}.", usuarioEncontrado.Id);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            var accessToken = await tokenProvider.GerarAccessTokenAsync(usuarioEncontrado);

            var (refreshToken, refreshTokenBruto) = await refreshTokenProvider.GerarRefreshTokenAsync(usuarioEncontrado);

            return Result.Ok((accessToken, refreshToken, refreshTokenBruto));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ocorreu um erro durante a autenticação de {@Command}.",
                command
            );

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
