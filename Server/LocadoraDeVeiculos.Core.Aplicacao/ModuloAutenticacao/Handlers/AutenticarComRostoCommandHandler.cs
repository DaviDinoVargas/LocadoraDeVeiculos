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
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

public class AutenticarComRostoCommandHandler(
    UserManager<Usuario> userManager,
    IFacialAuthClient facialAuthClient,
    AccessTokenProvider tokenProvider,
    RefreshTokenProvider refreshTokenProvider,
    ILogger<AutenticarComRostoCommandHandler> logger
) : IRequestHandler<AutenticarComRostoCommand, Result<(AccessToken, RefreshToken, string)>>
{
    // Mensagem genérica igual à do login por senha: evita enumeração de e-mails e,
    // aqui também, evita revelar se o e-mail tem ou não rosto cadastrado.
    private const string MensagemGenericaDeFalha = "Não foi possível autenticar com reconhecimento facial.";

    private const string MensagemContaBloqueada =
        "Conta temporariamente bloqueada por excesso de tentativas. Tente novamente mais tarde.";

    public async Task<Result<(AccessToken, RefreshToken, string)>> Handle(
        AutenticarComRostoCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var usuarioEncontrado = await userManager.FindByEmailAsync(command.Email);

            if (usuarioEncontrado is null)
            {
                logger.LogWarning("Tentativa de login facial com e-mail não cadastrado: {Email}.", MascaramentoDeDados.MascararEmail(command.Email));
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            // Mesmo bloqueio do login por senha: sem isto o rosto seria um caminho sem limite de
            // tentativas para quem só conhece o e-mail (o rate limit por IP não basta).
            if (await userManager.IsLockedOutAsync(usuarioEncontrado))
            {
                logger.LogWarning("Login facial bloqueado por excesso de tentativas para o usuário {UserId}.", usuarioEncontrado.Id);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemContaBloqueada));
            }

            var personId = FacialAuthClient.PersonIdParaEmail(command.Email);

            var verificacao = await facialAuthClient.VerificarAsync(personId, command.ImagemBase64, cancellationToken);

            if (!verificacao.Encontrado || !verificacao.Bateu)
            {
                // "Sem rosto cadastrado" e "rosto diferente" têm a mesma resposta e o mesmo custo
                // (uma falha no contador). Foto sem rosto detectável é problema da imagem, não é
                // tentativa de acesso, e não conta.
                if (!verificacao.NenhumRostoNaImagem)
                    await userManager.AccessFailedAsync(usuarioEncontrado);

                logger.LogWarning(
                    "Login facial recusado para o usuário {UserId} (encontrado={Encontrado}, confiança={Confianca}).",
                    usuarioEncontrado.Id, verificacao.Encontrado, verificacao.Confianca);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            await userManager.ResetAccessFailedCountAsync(usuarioEncontrado);

            var accessToken = await tokenProvider.GerarAccessTokenAsync(usuarioEncontrado);

            var (refreshToken, refreshTokenBruto) = await refreshTokenProvider.GerarRefreshTokenAsync(usuarioEncontrado);

            return Result.Ok((accessToken, refreshToken, refreshTokenBruto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro durante a autenticação facial de {Comando}.", command.GetType().Name);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
