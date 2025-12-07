using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;

public record EditarFuncionarioCommand(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc
) : IRequest<Result<EditarFuncionarioResult>>;

public record EditarFuncionarioResult(
    string NomeCompleto,
    string Cpf,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc
);
