using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;

public record SelecionarFuncionarioPorIdQuery(Guid Id) : IRequest<Result<SelecionarFuncionarioPorIdResult>>;

public record SelecionarFuncionarioPorIdResult(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    string Email,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc
);
