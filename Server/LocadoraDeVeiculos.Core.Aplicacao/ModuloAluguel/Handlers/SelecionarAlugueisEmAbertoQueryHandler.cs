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
    public class SelecionarAlugueisEmAbertoQueryHandler : IRequestHandler<SelecionarAlugueisEmAbertoQuery, Result<SelecionarAlugueisEmAbertoResult>>
    {
        private readonly IRepositorioAluguel _repositorioAluguel;

        public SelecionarAlugueisEmAbertoQueryHandler(IRepositorioAluguel repositorioAluguel)
        {
            _repositorioAluguel = repositorioAluguel;
        }

        public async Task<Result<SelecionarAlugueisEmAbertoResult>> Handle(
            SelecionarAlugueisEmAbertoQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioAluguel.SelecionarAlugueisEmAbertoAsync();

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

            var response = new SelecionarAlugueisEmAbertoResult(dtos);

            return Result.Ok(response);
        }
    }
}