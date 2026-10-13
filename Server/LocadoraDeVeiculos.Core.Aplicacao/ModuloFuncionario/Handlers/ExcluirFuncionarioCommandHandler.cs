using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Handlers;

public class ExcluirFuncionarioCommandHandler(
    LocadoraDeVeiculosDbContext dbContext,
    RepositorioFuncionarioEmOrm repositorioFuncionario,
    UserManager<Usuario> userManager,
    IEncerradorDeAcesso encerradorDeAcesso,
    ILogger<ExcluirFuncionarioCommandHandler> logger
) : IRequestHandler<ExcluirFuncionarioCommand, Result<ExcluirFuncionarioResult>>
{
    public async Task<Result<ExcluirFuncionarioResult>> Handle(
        ExcluirFuncionarioCommand command, CancellationToken cancellationToken)
    {
        try
        {
            Funcionario? funcionario = await repositorioFuncionario.SelecionarPorIdAsync(command.Id);

            if (funcionario is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            Usuario? usuario = await userManager.FindByIdAsync(funcionario.UsuarioId.ToString());

            if (usuario is not null)
                await encerradorDeAcesso.EncerrarAsync(usuario, "Exclusão", cancellationToken);

            await repositorioFuncionario.ExcluirAsync(funcionario.Id);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok(new ExcluirFuncionarioResult());
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ocorreu um erro durante a exclusão de de {Comando}.",
                command.GetType().Name);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}