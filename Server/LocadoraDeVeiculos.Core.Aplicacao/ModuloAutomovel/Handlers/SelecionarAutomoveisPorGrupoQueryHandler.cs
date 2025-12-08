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
    public class SelecionarAutomoveisPorGrupoQueryHandler : IRequestHandler<SelecionarAutomoveisPorGrupoQuery, Result<SelecionarAutomoveisPorGrupoResult>>
    {
        private readonly IRepositorioAutomovel _repositorioAutomovel;

        public SelecionarAutomoveisPorGrupoQueryHandler(IRepositorioAutomovel repositorioAutomovel)
        {
            _repositorioAutomovel = repositorioAutomovel;
        }

        public async Task<Result<SelecionarAutomoveisPorGrupoResult>> Handle(
            SelecionarAutomoveisPorGrupoQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioAutomovel.SelecionarPorGrupoAsync(query.GrupoId);

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

            var response = new SelecionarAutomoveisPorGrupoResult(dtos);

            return Result.Ok(response);
        }
    }
}