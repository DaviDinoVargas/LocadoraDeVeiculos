using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

// O navegador nunca fala com /face/* do serviço de ML (o módulo exige um segredo que só esta
// API conhece). Estes handlers são a ponte autenticada: status, conferência e remoção do rosto.

public class ObterStatusRostoQueryHandler(
    UserManager<Usuario> userManager,
    IFacialAuthClient facialAuthClient,
    ILogger<ObterStatusRostoQueryHandler> logger
) : IRequestHandler<ObterStatusRostoQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(ObterStatusRostoQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await userManager.FindByIdAsync(query.UsuarioId.ToString());

            if (usuario?.Email is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.UsuarioId));

            var personId = FacialAuthClient.PersonIdParaEmail(usuario.Email);

            return Result.Ok(await facialAuthClient.PossuiRostoAsync(personId, cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao consultar o rosto do usuário {UsuarioId}.", query.UsuarioId);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}

public class VerificarRostoCommandHandler(
    UserManager<Usuario> userManager,
    IFacialAuthClient facialAuthClient,
    ILogger<VerificarRostoCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<VerificarRostoCommand, Result<VerificacaoRostoResultado>>
{
    public async Task<Result<VerificacaoRostoResultado>> Handle(VerificarRostoCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

            if (usuario?.Email is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.UsuarioId));

            var personId = FacialAuthClient.PersonIdParaEmail(usuario.Email);

            var verificacao = await facialAuthClient.VerificarAsync(personId, command.ImagemBase64, cancellationToken);

            if (!verificacao.Encontrado)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                    "Nenhum rosto cadastrado ainda. Cadastre o seu rosto primeiro."));

            if (verificacao.NenhumRostoNaImagem)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                    "Nenhum rosto detectado na imagem. Aproxime-se da câmera e tente novamente."));

            if (auditoria is not null)
                await auditoria.RegistrarAsync(
                    verificacao.Bateu ? EventosDeAutenticacao.RostoVerificado : EventosDeAutenticacao.RostoNaoConfere, usuario, cancellationToken);

            return Result.Ok(new VerificacaoRostoResultado(verificacao.Bateu, verificacao.Confianca));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao verificar o rosto do usuário {UsuarioId}.", command.UsuarioId);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}

public class RemoverRostoCommandHandler(
    UserManager<Usuario> userManager,
    IFacialAuthClient facialAuthClient,
    ILogger<RemoverRostoCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<RemoverRostoCommand, Result>
{
    public async Task<Result> Handle(RemoverRostoCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

            if (usuario?.Email is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.UsuarioId));

            var personId = FacialAuthClient.PersonIdParaEmail(usuario.Email);

            var removido = await facialAuthClient.RemoverAsync(personId, cancellationToken);

            if (!removido)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                    "Não foi possível apagar o rosto agora. Tente novamente em instantes."));

            logger.LogInformation("Rosto removido a pedido do usuário {UsuarioId}.", command.UsuarioId);

            if (auditoria is not null)
                await auditoria.RegistrarAsync(EventosDeAutenticacao.RostoRemovido, usuario, cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao remover o rosto do usuário {UsuarioId}.", command.UsuarioId);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
