using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca
{
    public class RepositorioPlanoCobrancaEmOrm : RepositorioBaseEmOrm<PlanoCobranca>, IRepositorioPlanoCobranca
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioPlanoCobrancaEmOrm(LocadoraDeVeiculosDbContext dbContext)
            : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<PlanoCobranca?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.PlanoCobranca
                .Include(p => p.Empresa)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public override async Task<List<PlanoCobranca>> SelecionarTodosAsync()
        {
            return await dbContext.PlanoCobranca
                .Include(p => p.Empresa)
                .ToListAsync();
        }

        public Task<List<PlanoCobranca>> SelecionarTodosAsync(int quantity)
        {
            throw new NotImplementedException();
        }
    }
}
