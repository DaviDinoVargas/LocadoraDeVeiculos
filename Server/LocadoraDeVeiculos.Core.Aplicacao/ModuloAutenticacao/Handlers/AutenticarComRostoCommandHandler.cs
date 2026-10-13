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
    ILogger<AutenticarComRostoCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null,
    VerificadorDeSegundoFator? segundoFator = null,
    LimitadorDeLoginPorConta? limitador = null,
    ITenantProvider? tenant = null
) : IRequestHandler<AutenticarComRostoCommand, Result<(AccessToken, RefreshToken, string)>>
{
    // Mensagem genérica igual à do login por senha: evita enumeração de e-mails e,
    // aqui também, evita revelar se o e-mail tem ou não rosto cadastrado.
    private const string MensagemGenericaDeFalha = "Não foi possível autenticar com reconhecimento facial.";


    private Task Auditar(string evento, Usuario usuario, CancellationToken cancellationToken) =>
        auditoria?.RegistrarAsync(evento, usuario, cancellationToken) ?? Task.CompletedTask;

    private string? Ip => tenant?.EnderecoIp;

    public async Task<Result<(AccessToken, RefreshToken, string)>> Handle(
        AutenticarComRostoCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // S10/S28: mesmo limite por (conta, IP) do login por senha, sem somar erro à conta.
            if (limitador?.EstaBloqueado(command.Email, Ip) == true)
            {
                logger.LogWarning("Login facial recusado: limite de erros por conta e IP atingido para {Email}.", MascaramentoDeDados.MascararEmail(command.Email));
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

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
                await Auditar(EventosDeAutenticacao.LoginFacialBloqueado, usuarioEncontrado, cancellationToken);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            var personId = FacialAuthClient.PersonIdParaEmail(command.Email);

            var verificacao = await facialAuthClient.VerificarAsync(personId, command.ImagemBase64, cancellationToken);

            if (!verificacao.Encontrado || !verificacao.Bateu)
            {
                // "Sem rosto cadastrado" e "rosto diferente" têm a mesma resposta e o mesmo custo
                // (uma falha no contador). Foto sem rosto detectável é problema da imagem, não é
                // tentativa de acesso, e não conta.
                if (!verificacao.NenhumRostoNaImagem)
                {
                    await userManager.AccessFailedAsync(usuarioEncontrado);
                    limitador?.RegistrarFalha(command.Email, Ip);
                }

                await Auditar(EventosDeAutenticacao.LoginFacialFalhou, usuarioEncontrado, cancellationToken);

                logger.LogWarning(
                    "Login facial recusado para o usuário {UserId} (encontrado={Encontrado}, confiança={Confianca}, provaDeVidaReprovada={Vida}).",
                    usuarioEncontrado.Id, verificacao.Encontrado, verificacao.Confianca, verificacao.ProvaDeVidaReprovada);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
            }

            // Quem ativou o 2FA não entra só com o rosto: o código do aplicativo também é exigido.
            if (segundoFator is not null)
            {
                var segundo = await segundoFator.VerificarAsync(usuarioEncontrado, command.CodigoAutenticador);

                if (segundo == ResultadoDoSegundoFator.CodigoObrigatorio)
                    return Result.Fail(ResultadosErro.SegundoFatorObrigatorioErro());

                if (segundo == ResultadoDoSegundoFator.CodigoInvalido)
                {
                    await Auditar(EventosDeAutenticacao.LoginFacialFalhou, usuarioEncontrado, cancellationToken);
                    limitador?.RegistrarFalha(command.Email, Ip);

                    return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(MensagemGenericaDeFalha));
                }
            }

            await userManager.ResetAccessFailedCountAsync(usuarioEncontrado);
            limitador?.Zerar(command.Email, Ip);

            var accessToken = await tokenProvider.GerarAccessTokenAsync(usuarioEncontrado);

            var (refreshToken, refreshTokenBruto) = await refreshTokenProvider.GerarRefreshTokenAsync(usuarioEncontrado);

            await Auditar(EventosDeAutenticacao.LoginFacialOk, usuarioEncontrado, cancellationToken);

            return Result.Ok((accessToken, refreshToken, refreshTokenBruto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro durante a autenticação facial de {Comando}.", command.GetType().Name);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
