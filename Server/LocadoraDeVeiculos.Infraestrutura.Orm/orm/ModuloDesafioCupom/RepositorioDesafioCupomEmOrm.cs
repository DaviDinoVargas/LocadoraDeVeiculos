using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDesafioCupom
{
    public class RepositorioDesafioCupomEmOrm : RepositorioBaseEmOrm<DesafioCupom>, IRepositorioDesafioCupom
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioDesafioCupomEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<DesafioCupom?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Set<DesafioCupom>()
                .Include(d => d.CupomRecompensa)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public override async Task<List<DesafioCupom>> SelecionarTodosAsync()
        {
            return await dbContext.Set<DesafioCupom>()
                .Include(d => d.CupomRecompensa)
                .OrderBy(d => d.Nome)
                .ToListAsync();
        }

        public async Task<int> ContarAlugueisConcluidosDoClienteAsync(Guid clienteId, int periodoDias)
        {
            var desde = DateTimeOffset.UtcNow.AddDays(-periodoDias);

            return await dbContext.Set<Aluguel>().CountAsync(a =>
                a.ClienteId == clienteId &&
                a.Status == StatusAluguel.Concluido &&
                a.DataSaida >= desde);
        }
    }
}
