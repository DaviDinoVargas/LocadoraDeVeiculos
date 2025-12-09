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
    public class SelecionarTaxasServicoQueryHandler : IRequestHandler<SelecionarTaxasServicoQuery, Result<SelecionarTaxasServicoResult>>
    {
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;

        public SelecionarTaxasServicoQueryHandler(IRepositorioTaxaServico repositorioTaxaServico)
        {
            _repositorioTaxaServico = repositorioTaxaServico;
        }

        public async Task<Result<SelecionarTaxasServicoResult>> Handle(
            SelecionarTaxasServicoQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioTaxaServico.SelecionarTodosAsync();

            var dtos = registros
                .Select(t => new SelecionarTaxasServicoDto(
                    t.Id,
                    t.Nome,
                    t.Preco,
                    t.TipoCalculo.ToString()
                ))
                .ToImmutableList();

            var response = new SelecionarTaxasServicoResult(dtos);

            return Result.Ok(response);
        }
    }
}