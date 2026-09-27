using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCupom
{
    public class RepositorioCupomEmOrm : RepositorioBaseEmOrm<Cupom>, IRepositorioCupom
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioCupomEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<Cupom?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Set<Cupom>()
                .Include(c => c.Parceiro)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public override async Task<List<Cupom>> SelecionarTodosAsync()
        {
            return await dbContext.Set<Cupom>()
                .Include(c => c.Parceiro)
                .OrderByDescending(c => c.CriadoEmUtc)
                .ToListAsync();
        }

        public async Task<bool> ExisteCupomComCodigoAsync(string codigo, Guid? idExcluir = null)
        {
            var codigoNormalizado = codigo.ToUpperInvariant();
            var query = dbContext.Set<Cupom>().Where(c => c.Codigo == codigoNormalizado);

            if (idExcluir.HasValue)
                query = query.Where(c => c.Id != idExcluir.Value);

            return await query.AnyAsync();
        }

        public async Task<Cupom?> SelecionarPorCodigoAsync(string codigo)
        {
            var codigoNormalizado = codigo.ToUpperInvariant();

            return await dbContext.Set<Cupom>()
                .FirstOrDefaultAsync(c => c.Codigo == codigoNormalizado);
        }

        public async Task<bool> ExisteAluguelVinculadoAsync(Guid cupomId)
        {
            return await dbContext.Set<Aluguel>().AnyAsync(a =>
                a.CupomId == cupomId &&
                (a.Status == StatusAluguel.Reservado || a.Status == StatusAluguel.EmAndamento));
        }
    }
}
