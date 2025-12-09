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
    public class SelecionarAlugueisQueryHandler : IRequestHandler<SelecionarAlugueisQuery, Result<SelecionarAlugueisResult>>
    {
        private readonly IRepositorioAluguel _repositorioAluguel;

        public SelecionarAlugueisQueryHandler(IRepositorioAluguel repositorioAluguel)
        {
            _repositorioAluguel = repositorioAluguel;
        }

        public async Task<Result<SelecionarAlugueisResult>> Handle(
            SelecionarAlugueisQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioAluguel.SelecionarTodosAsync();

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

            var response = new SelecionarAlugueisResult(dtos);

            return Result.Ok(response);
        }
    }
}