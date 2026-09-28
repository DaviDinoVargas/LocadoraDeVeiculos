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

public class CadastrarRostoCommandHandler(
    UserManager<Usuario> userManager,
    IFacialAuthClient facialAuthClient,
    ILogger<CadastrarRostoCommandHandler> logger
) : IRequestHandler<CadastrarRostoCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CadastrarRostoCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

            if (usuario?.Email is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.UsuarioId));

            var personId = FacialAuthClient.PersonIdParaEmail(usuario.Email);

            var resultado = await facialAuthClient.CadastrarAsync(personId, command.ImagemBase64, cancellationToken);

            if (!resultado.Sucesso)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                    "Não foi possível cadastrar o rosto. Verifique se a imagem mostra um rosto claramente."));

            return Result.Ok(resultado.Amostras);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao cadastrar rosto para o usuário {UsuarioId}.", command.UsuarioId);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
