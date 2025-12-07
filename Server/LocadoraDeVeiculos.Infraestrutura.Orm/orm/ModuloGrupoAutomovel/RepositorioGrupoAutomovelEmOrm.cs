using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloGrupoAutomovel
{
    public class RepositorioGrupoAutomovelEmOrm : RepositorioBaseEmOrm<GrupoAutomovel>, IRepositorioGrupoAutomovel
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioGrupoAutomovelEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<GrupoAutomovel?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.GruposAutomovel
                .FirstOrDefaultAsync(g => g.Id == id);
        }

        public override async Task<List<GrupoAutomovel>> SelecionarTodosAsync()
        {
            return await dbContext.GruposAutomovel
                .OrderBy(g => g.Nome)
                .ToListAsync();
        }

        public async Task<bool> ExisteGrupoComNomeAsync(string nome, Guid? idExcluir = null)
        {
            var query = dbContext.GruposAutomovel
                .Where(g => g.Nome.ToLower() == nome.ToLower());

            if (idExcluir.HasValue)
            {
                query = query.Where(g => g.Id != idExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteAutomovelVinculadoAsync(Guid grupoId)
        {
            // Esta verificação será implementada quando tivermos o módulo de Automóveis
            // Por enquanto, retorna false
            return await Task.FromResult(false);
        }

        public async Task<bool> ExistePlanoCobrancaVinculadoAsync(Guid grupoId)
        {
            // Esta verificação será implementada quando tivermos o módulo de Planos de Cobrança
            // Por enquanto, retorna false
            return await Task.FromResult(false);
        }

        public Task<List<GrupoAutomovel>> SelecionarTodosAsync(int quantity)
        {
            throw new NotImplementedException();
        }
    }
}