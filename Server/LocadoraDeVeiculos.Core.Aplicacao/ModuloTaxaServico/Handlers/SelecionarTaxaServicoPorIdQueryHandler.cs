using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Handlers
{
    public class SelecionarTaxaServicoPorIdQueryHandler : IRequestHandler<SelecionarTaxaServicoPorIdQuery, Result<SelecionarTaxaServicoPorIdResult>>
    {
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;

        public SelecionarTaxaServicoPorIdQueryHandler(IRepositorioTaxaServico repositorioTaxaServico)
        {
            _repositorioTaxaServico = repositorioTaxaServico;
        }

        public async Task<Result<SelecionarTaxaServicoPorIdResult>> Handle(
            SelecionarTaxaServicoPorIdQuery query, CancellationToken cancellationToken)
        {
            var taxaServico = await _repositorioTaxaServico.SelecionarPorIdAsync(query.Id);

            if (taxaServico is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            var result = new SelecionarTaxaServicoPorIdResult(
                taxaServico.Id,
                taxaServico.Nome,
                taxaServico.Preco,
                taxaServico.TipoCalculo
            );

            return Result.Ok(result);
        }
    }
}