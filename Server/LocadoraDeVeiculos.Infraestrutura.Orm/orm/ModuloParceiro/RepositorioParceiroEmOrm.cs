using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloParceiro
{
    public class RepositorioParceiroEmOrm : RepositorioBaseEmOrm<Parceiro>, IRepositorioParceiro
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioParceiroEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<Parceiro?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Set<Parceiro>().FirstOrDefaultAsync(p => p.Id == id);
        }

        public override async Task<List<Parceiro>> SelecionarTodosAsync()
        {
            return await dbContext.Set<Parceiro>().OrderBy(p => p.Nome).ToListAsync();
        }

        public async Task<bool> ExisteParceiroComCnpjAsync(string cnpj, Guid? idExcluir = null)
        {
            var query = dbContext.Set<Parceiro>().Where(p => p.Cnpj == cnpj);

            if (idExcluir.HasValue)
                query = query.Where(p => p.Id != idExcluir.Value);

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteCupomVinculadoAsync(Guid parceiroId)
        {
            return await dbContext.Set<Cupom>().AnyAsync(c => c.ParceiroId == parceiroId);
        }
    }
}
