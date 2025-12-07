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
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Handlers;

public class CadastrarFuncionarioCommandHandler(
    LocadoraDeVeiculosDbContext appDbContext,
    RepositorioFuncionarioEmOrm repositorioFuncionario,
    UserManager<Usuario> userManager,
    RoleManager<Cargo> roleManager,
    ITenantProvider tenantProvider,
    IValidator<CadastrarFuncionarioCommand> validator,
    ILogger<CadastrarFuncionarioCommandHandler> logger
) : IRequestHandler<CadastrarFuncionarioCommand, Result<CadastrarFuncionarioResult>>
{
    public async Task<Result<CadastrarFuncionarioResult>> Handle(
        CadastrarFuncionarioCommand command, CancellationToken cancellationToken)
    {
        List<Funcionario> registros = await repositorioFuncionario.SelecionarTodosAsync();

        if (registros.Any(x => x.Cpf.Equals(command.Cpf)))
            return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Um funcionário com este CPF já está cadastrado."));

        ValidationResult resultadoValidacao = await validator.ValidateAsync(command, cancellationToken);

        if (!resultadoValidacao.IsValid)
        {
            var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);

            return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
        }

        Usuario? usuario = null;

        try
        {
            usuario = new Usuario()
            {
                FullName = command.NomeCompleto,
                UserName = command.Email,
                Email = command.Email
            };

            var resultadoCriacaoUsuario =
                await userManager.CreateAsync(usuario, command.ConfirmarSenha);

            if (!resultadoCriacaoUsuario.Succeeded)
            {
                var erros = resultadoCriacaoUsuario.Errors.Select(e => e.Description);

                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            var cargoStr = CargoUsuario.Funcionario.ToString();

            var resultadoBuscaCargo = await roleManager.FindByNameAsync(cargoStr);

            if (resultadoBuscaCargo is null)
            {
                var cargo = new Cargo()
                {
                    Name = cargoStr,
                    NormalizedName = cargoStr.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };

                await roleManager.CreateAsync(cargo);
            }

            var resultadoInclusaoCargo = await userManager.AddToRoleAsync(usuario, cargoStr);

            if (!resultadoInclusaoCargo.Succeeded)
            {
                var erros = resultadoInclusaoCargo.Errors.Select(e => e.Description);

                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            var funcionario = new Funcionario(
               usuario.Id,
               tenantProvider.EmpresaId.GetValueOrDefault(),
               command.NomeCompleto,
               command.Cpf,
               command.Email,
               command.Salario,
               command.AdmissaoEmUtc
            );

            await repositorioFuncionario.CadastrarAsync(funcionario);

            await appDbContext.SaveChangesAsync(cancellationToken);

            var resultado = new CadastrarFuncionarioResult(funcionario.Id);

            return Result.Ok(resultado);
        }
        catch (Exception ex)
        {
            if (usuario is not null)
                await userManager.DeleteAsync(usuario);

            logger.LogError(
                ex,
                "Ocorreu um erro durante o cadastro de {@Command}.",
                command
            );

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
