using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Handlers;

public class SelecionarFuncionarioPorIdQueryHandler(RepositorioFuncionarioEmOrm repositorioFuncionario)
    : IRequestHandler<SelecionarFuncionarioPorIdQuery, Result<SelecionarFuncionarioPorIdResult>>
{
    public async Task<Result<SelecionarFuncionarioPorIdResult>> Handle(
        SelecionarFuncionarioPorIdQuery query, CancellationToken cancellationToken)
    {
        Funcionario? registroEncontrado = await repositorioFuncionario.SelecionarPorIdAsync(query.Id);

        if (registroEncontrado is null)
            return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

        SelecionarFuncionarioPorIdResult result = new(
            registroEncontrado.Id,
            registroEncontrado.NomeCompleto,
            registroEncontrado.Cpf,
            registroEncontrado.Email,
            registroEncontrado.Salario,
            registroEncontrado.AdmissaoEmUtc
        );

        return Result.Ok(result);
    }
}
