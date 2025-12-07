using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Handlers;

public class EditarFuncionarioCommandHandler(
    LocadoraDeVeiculosDbContext appDbContext,
    RepositorioFuncionarioEmOrm repositorioFuncionario,
    ITenantProvider tenantProvider,
    IValidator<EditarFuncionarioCommand> validator,
    ILogger<EditarFuncionarioCommandHandler> logger
) : IRequestHandler<EditarFuncionarioCommand, Result<EditarFuncionarioResult>>
{
    public async Task<Result<EditarFuncionarioResult>> Handle(
        EditarFuncionarioCommand command, CancellationToken cancellationToken)
    {
        Funcionario? registroEncontrado = await repositorioFuncionario.SelecionarPorIdAsync(command.Id);

        if (registroEncontrado is null)
            return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

        ValidationResult resultadoValidacao = await validator.ValidateAsync(command, cancellationToken);

        if (!resultadoValidacao.IsValid)
        {
            var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);

            return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
        }

        List<Funcionario> registros = await repositorioFuncionario.SelecionarTodosAsync();

        if (registros.Any(x => !x.Id.Equals(command.Id) && x.Cpf.Equals(command.Cpf)))
            return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Um funcionário com este CPF já está cadastrado."));

        try
        {
            Funcionario funcionarioEditado = new(
                registroEncontrado.UsuarioId,
                tenantProvider.EmpresaId.GetValueOrDefault(),
                command.NomeCompleto,
                command.Cpf,
                registroEncontrado.Email,
                command.Salario,
                command.AdmissaoEmUtc
            );

            await repositorioFuncionario.EditarAsync(command.Id, funcionarioEditado);

            await appDbContext.SaveChangesAsync(cancellationToken);

            EditarFuncionarioResult result = new(
                command.NomeCompleto,
                command.Cpf,
                command.Salario,
                command.AdmissaoEmUtc
            );

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ocorreu um erro durante a edição de {@Command}.",
                command
            );

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
