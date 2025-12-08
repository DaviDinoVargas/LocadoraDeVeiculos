using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel
{
    public class RepositorioAutomovelEmOrm : RepositorioBaseEmOrm<Automovel>, IRepositorioAutomovel
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioAutomovelEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<Automovel?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Automoveis
                .Include(a => a.GrupoAutomovel)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public override async Task<List<Automovel>> SelecionarTodosAsync()
        {
            return await dbContext.Automoveis
                .Include(a => a.GrupoAutomovel)
                .OrderBy(a => a.Placa)
                .ToListAsync();
        }

        public async Task<bool> ExisteAutomovelComPlacaAsync(string placa, Guid? idExcluir = null)
        {
            var query = dbContext.Automoveis
                .Where(a => a.Placa.ToLower() == placa.ToLower());

            if (idExcluir.HasValue)
            {
                query = query.Where(a => a.Id != idExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteAluguelEmAbertoAsync(Guid automovelId)
        {
            // Esta verificação será implementada quando tivermos o módulo de Aluguéis
            // Por enquanto, retorna false
            return await Task.FromResult(false);
        }

        public async Task<List<Automovel>> SelecionarPorGrupoAsync(Guid grupoId)
        {
            return await dbContext.Automoveis
                .Include(a => a.GrupoAutomovel)
                .Where(a => a.GrupoAutomovelId == grupoId)
                .OrderBy(a => a.Placa)
                .ToListAsync();
        }
    }
}