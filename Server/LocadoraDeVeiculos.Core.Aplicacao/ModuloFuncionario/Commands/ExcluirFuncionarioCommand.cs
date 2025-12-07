using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;

public record ExcluirFuncionarioCommand(Guid Id) : IRequest<Result<ExcluirFuncionarioResult>>;

public record ExcluirFuncionarioResult();
