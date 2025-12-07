using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloFuncionario;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Controllers;

[Authorize(Roles = "Empresa")]
[Route("api/funcionarios")]
public sealed class FuncionarioController(IMediator mediator) : MainController
{
    [HttpPost]
    public async Task<ActionResult<CadastrarFuncionarioRequest>> Cadastrar(
        CadastrarFuncionarioRequest request,
        CancellationToken cancellationToken
    )
    {
        CadastrarFuncionarioCommand command = new(
            request.NomeCompleto,
            request.Cpf,
            request.Email,
            request.Senha,
            request.ConfirmarSenha,
            request.Salario,
            request.AdmissaoEmUtc
        );

        Result<CadastrarFuncionarioResult> result = await mediator.Send(command, cancellationToken);

        return ProcessarResultado(result, (valor) =>
        {
            CadastrarFuncionarioResponse response = new(valor.Id);

            return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EditarFuncionarioResponse>> Editar(
       Guid id,
       EditarFuncionarioRequest request,
       CancellationToken cancellationToken
    )
    {
        EditarFuncionarioCommand command = new(
            id,
            request.NomeCompleto,
            request.Cpf,
            request.Salario,
            request.AdmissaoEmUtc
        );

        Result<EditarFuncionarioResult> result = await mediator.Send(command, cancellationToken);

        return ProcessarResultado(result, (valor) =>
        {
            EditarFuncionarioResponse response = new(valor.NomeCompleto, valor.Cpf, valor.Salario, valor.AdmissaoEmUtc);

            return Ok(response);
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ExcluirFuncionarioResponse>> Excluir(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        ExcluirFuncionarioCommand command = new(id);

        Result<ExcluirFuncionarioResult> result = await mediator.Send(command, cancellationToken);

        return ProcessarResultado(result, (_) => NoContent());
    }

    [HttpGet]
    public async Task<ActionResult<SelecionarFuncionariosResponse>> SelecionarTodos(
        CancellationToken cancellationToken
    )
    {
        SelecionarFuncionariosQuery query = new();

        Result<SelecionarFuncionariosResult> result = await mediator.Send(query, cancellationToken);

        return ProcessarResultado(result, (valor) =>
        {
            SelecionarFuncionariosResponse response = new(valor.Registros);

            return Ok(response);
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SelecionarFuncionarioPorIdResponse>> SelecionarPorId(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        SelecionarFuncionarioPorIdQuery query = new(id);

        Result<SelecionarFuncionarioPorIdResult> result = await mediator.Send(query, cancellationToken);

        return ProcessarResultado(result, (valor) =>
        {
            SelecionarFuncionarioPorIdResponse response = new(
                valor.Id,
                valor.NomeCompleto,
                valor.Cpf,
                valor.Email,
                valor.Salario,
                valor.AdmissaoEmUtc
            );

            return Ok(response);
        });
    }
}
