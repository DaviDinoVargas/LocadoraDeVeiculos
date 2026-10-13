using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
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

public class EditarFuncionarioCommandHandler(
    LocadoraDeVeiculosDbContext appDbContext,
    RepositorioFuncionarioEmOrm repositorioFuncionario,
    ITenantProvider tenantProvider,
    UserManager<Usuario> userManager,
    RefreshTokenProvider refreshTokenProvider,
    IValidator<EditarFuncionarioCommand> validator,
    ILogger<EditarFuncionarioCommandHandler> logger
) : IRequestHandler<EditarFuncionarioCommand, Result<EditarFuncionarioResult>>
{
    /// <summary>
    /// A empresa redefine a senha do funcionário (S27): vale a mesma política, e as sessões dele são derrubadas para que
    /// uma senha vazada ou de quem saiu deixe de servir na hora.
    /// </summary>
    private async Task<IEnumerable<string>?> RedefinirSenhaAsync(Guid usuarioId, string novaSenha)
    {
        var usuario = await userManager.FindByIdAsync(usuarioId.ToString());

        if (usuario is null)
            return ["Usuário do funcionário não encontrado."];

        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
        var resultado = await userManager.ResetPasswordAsync(usuario, token, novaSenha);

        if (!resultado.Succeeded)
            return resultado.Errors.Select(e => e.Description).ToList();

        usuario.AccessTokenVersionId = Guid.NewGuid();
        await userManager.UpdateAsync(usuario);
        await userManager.ResetAccessFailedCountAsync(usuario);
        await refreshTokenProvider.RevogarTokensUsuarioAsync(usuario.Id, "Senha redefinida pela empresa");

        return null;
    }

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

            if (!string.IsNullOrEmpty(command.Senha))
            {
                var erroSenha = await RedefinirSenhaAsync(registroEncontrado.UsuarioId, command.Senha);

                if (erroSenha is not null)
                    return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erroSenha));
            }

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
                "Ocorreu um erro durante a edição de {Comando}.",
                command.GetType().Name);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
