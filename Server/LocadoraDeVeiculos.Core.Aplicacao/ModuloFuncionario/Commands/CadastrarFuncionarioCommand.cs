using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;

public record CadastrarFuncionarioCommand(
    string NomeCompleto,
    string Cpf,
    string Email,
    string Senha,
    string ConfirmarSenha,
    decimal Salario,
    DateTimeOffset AdmissaoEmUtc
) : IRequest<Result<CadastrarFuncionarioResult>>;

public record CadastrarFuncionarioResult(Guid Id);
