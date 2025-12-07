using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;

public record SelecionarFuncionariosQuery() : IRequest<Result<SelecionarFuncionariosResult>>;

public record SelecionarFuncionariosResult(IReadOnlyList<SelecionarFuncionariosDto> Registros);

public record SelecionarFuncionariosDto(
    Guid Id,
    string NomeCompleto,
    string Email,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc
);
