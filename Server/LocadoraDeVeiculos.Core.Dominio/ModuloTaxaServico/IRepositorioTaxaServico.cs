using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico
{
    public interface IRepositorioTaxaServico : IRepositorio<TaxaServico>
    {
        Task<bool> ExisteTaxaServicoComNomeAsync(string nome, Guid? idExcluir = null);
        Task<bool> ExisteAluguelVinculadoAsync(Guid taxaServicoId);
        Task<List<TaxaServico>> SelecionarPorTipoAsync(TipoCalculo tipoCalculo);
        Task<List<TaxaServico>> SelecionarPorIdsAsync(List<Guid> ids);
    }
}