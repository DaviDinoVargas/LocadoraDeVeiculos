using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico
{
    public class RepositorioTaxaServicoEmOrm : RepositorioBaseEmOrm<TaxaServico>, IRepositorioTaxaServico
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioTaxaServicoEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<TaxaServico?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.TaxasServico
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public override async Task<List<TaxaServico>> SelecionarTodosAsync()
        {
            return await dbContext.TaxasServico
                .OrderBy(t => t.Nome)
                .ToListAsync();
        }

        public async Task<bool> ExisteTaxaServicoComNomeAsync(string nome, Guid? idExcluir = null)
        {
            var query = dbContext.TaxasServico
                .Where(t => t.Nome.ToLower() == nome.ToLower());

            if (idExcluir.HasValue)
            {
                query = query.Where(t => t.Id != idExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteAluguelVinculadoAsync(Guid taxaServicoId)
        {
            // Esta verificação será implementada quando tivermos o módulo de Aluguéis
            // Por enquanto, retorna false
            return await Task.FromResult(false);
        }

        public async Task<List<TaxaServico>> SelecionarPorTipoAsync(TipoCalculo tipoCalculo)
        {
            return await dbContext.TaxasServico
                .Where(t => t.TipoCalculo == tipoCalculo)
                .OrderBy(t => t.Nome)
                .ToListAsync();
        }

        public Task<List<TaxaServico>> SelecionarTodosAsync(int quantity)
        {
            throw new NotImplementedException();
        }
    }
}