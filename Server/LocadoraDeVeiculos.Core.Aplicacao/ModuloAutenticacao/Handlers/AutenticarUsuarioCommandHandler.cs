using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
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
    ILogger<AutenticarUsuarioCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<AutenticarUsuarioCommand, Result<(AccessToken, RefreshToken, string)>>
{
    private const string MensagemGenericaDeFalha = "Usuário ou senha inválidos.";

    private Task Auditar(string evento, Usuario usuario, CancellationToken cancellationToken) =>
        auditoria?.RegistrarAsync(evento, usuario, cancellationToken) ?? Task.CompletedTask;

    public async Task<Result<(AccessToken, RefreshToken, string)>> Handle(
        AutenticarUsuarioCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var usuarioEncontrado = await userManager.FindByEmailAsync(command.Email);

            // Mensagem idêntica para "usuário não existe" e "senha incorreta": evita enumeração de e-mails.
            if (usuarioEncontrado is null)
            {
                logger.LogWarning("Tentativa de login com e-mail não cadastrado: {Email}.", MascaramentoDeDados.MascararEmail(command.Email));

                // Sem isto o e-mail inexistente responde em ~4 ms e o existente em ~120 ms (custo do PBKDF2):
                // dá para descobrir quem tem conta medindo o tempo. Gasta-se o mesmo custo de hash e descarta-se.
                EqualizadorDeTempoDeLogin.GastarCustoDeHash(userManager.PasswordHasher, command.Senha);

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
                await Auditar(EventosDeAutenticacao.LoginBloqueado, usuarioEncontrado, cancellationToken);
                // Mesma mensagem de "senha incorreta" e de "e-mail inexistente": uma mensagem própria para
                // bloqueio revelaria que o e-mail tem conta e permitiria confirmar que o ataque funcionou.
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            if (!resultadoLogin.Succeeded)
            {
                logger.LogWarning("Tentativa de login com senha incorreta para o usuário {UserId}.", usuarioEncontrado.Id);
                await Auditar(EventosDeAutenticacao.LoginFalhou, usuarioEncontrado, cancellationToken);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            var accessToken = await tokenProvider.GerarAccessTokenAsync(usuarioEncontrado);

            var (refreshToken, refreshTokenBruto) = await refreshTokenProvider.GerarRefreshTokenAsync(usuarioEncontrado);

            await Auditar(EventosDeAutenticacao.LoginOk, usuarioEncontrado, cancellationToken);

            return Result.Ok((accessToken, refreshToken, refreshTokenBruto));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ocorreu um erro durante a autenticação de {Comando}.",
                command.GetType().Name);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
