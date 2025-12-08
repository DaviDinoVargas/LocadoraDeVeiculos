using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using MediatR;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Handlers
{
    public class SelecionarAutomoveisQueryHandler : IRequestHandler<SelecionarAutomoveisQuery, Result<SelecionarAutomoveisResult>>
    {
        private readonly IRepositorioAutomovel _repositorioAutomovel;

        public SelecionarAutomoveisQueryHandler(IRepositorioAutomovel repositorioAutomovel)
        {
            _repositorioAutomovel = repositorioAutomovel;
        }

        public async Task<Result<SelecionarAutomoveisResult>> Handle(
            SelecionarAutomoveisQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioAutomovel.SelecionarTodosAsync();

            var dtos = registros
                .Select(r => new SelecionarAutomoveisDto(
                    r.Id,
                    r.Placa,
                    r.Marca,
                    r.Cor,
                    r.Modelo,
                    r.TipoCombustivel,
                    r.CapacidadeTanque,
                    r.Ano,
                    r.Foto,
                    r.GrupoAutomovelId,
                    r.GrupoAutomovel?.Nome ?? string.Empty
                ))
                .ToImmutableList();

            var response = new SelecionarAutomoveisResult(dtos);

            return Result.Ok(response);
        }
    }
}