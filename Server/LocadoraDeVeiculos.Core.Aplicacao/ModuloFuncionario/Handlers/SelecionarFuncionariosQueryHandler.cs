using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Handlers;

public class SelecionarFuncionariosQueryHandler(
    RepositorioFuncionarioEmOrm repositorioFuncionario
) : IRequestHandler<SelecionarFuncionariosQuery, Result<SelecionarFuncionariosResult>>
{
    public async Task<Result<SelecionarFuncionariosResult>> Handle(
        SelecionarFuncionariosQuery query, CancellationToken cancellationToken)
    {
        var registros = await repositorioFuncionario.SelecionarTodosAsync();

        var dtos = registros
            .Select(r => new SelecionarFuncionariosDto(
                r.Id,
                r.NomeCompleto,
                r.Email,
                r.Salario,
                r.AdmissaoEmUtc
            ))
            .ToImmutableList();

        var response = new SelecionarFuncionariosResult(dtos);

        return Result.Ok(response);
    }
}
