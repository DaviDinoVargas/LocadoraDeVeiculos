using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using MediatR;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Handlers
{
    public class SelecionarTaxasServicoPorTipoQueryHandler : IRequestHandler<SelecionarTaxasServicoPorTipoQuery, Result<SelecionarTaxasServicoPorTipoResult>>
    {
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;

        public SelecionarTaxasServicoPorTipoQueryHandler(IRepositorioTaxaServico repositorioTaxaServico)
        {
            _repositorioTaxaServico = repositorioTaxaServico;
        }

        public async Task<Result<SelecionarTaxasServicoPorTipoResult>> Handle(
            SelecionarTaxasServicoPorTipoQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioTaxaServico.SelecionarPorTipoAsync(query.TipoCalculo);

            var dtos = registros
                .Select(t => new SelecionarTaxasServicoDto(
                    t.Id,
                    t.Nome,
                    t.Preco,
                    t.TipoCalculo.ToString()
                ))
                .ToImmutableList();

            var response = new SelecionarTaxasServicoPorTipoResult(dtos);

            return Result.Ok(response);
        }
    }
}