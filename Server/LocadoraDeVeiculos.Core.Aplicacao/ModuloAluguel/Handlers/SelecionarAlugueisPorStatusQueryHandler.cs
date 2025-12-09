using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using MediatR;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers
{
    public class SelecionarAlugueisPorStatusQueryHandler : IRequestHandler<SelecionarAlugueisPorStatusQuery, Result<SelecionarAlugueisPorStatusResult>>
    {
        private readonly IRepositorioAluguel _repositorioAluguel;

        public SelecionarAlugueisPorStatusQueryHandler(IRepositorioAluguel repositorioAluguel)
        {
            _repositorioAluguel = repositorioAluguel;
        }

        public async Task<Result<SelecionarAlugueisPorStatusResult>> Handle(
            SelecionarAlugueisPorStatusQuery query, CancellationToken cancellationToken)
        {
            if (!System.Enum.TryParse<StatusAluguel>(query.Status, out var status))
                return Result.Fail("Status de aluguel inválido.");

            var registros = await _repositorioAluguel.SelecionarAlugueisPorStatusAsync(status);

            var dtos = registros
                .Select(a => new SelecionarAlugueisDto(
                    a.Id,
                    a.Condutor?.Nome ?? "N/A",
                    a.Automovel?.Placa ?? "N/A",
                    a.Cliente?.Nome ?? "N/A",
                    a.DataSaida,
                    a.DataRetornoPrevisto,
                    a.ValorPrevisto,
                    a.Status.ToString()
                ))
                .ToImmutableList();

            var response = new SelecionarAlugueisPorStatusResult(dtos);

            return Result.Ok(response);
        }
    }
}