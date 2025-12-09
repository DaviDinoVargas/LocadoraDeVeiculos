using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAluguel
{
    public class RepositorioAluguelEmOrm : RepositorioBaseEmOrm<Aluguel>, IRepositorioAluguel
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioAluguelEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<Aluguel?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Alugueis
                .Include(a => a.Condutor)
                .Include(a => a.Automovel)
                .ThenInclude(auto => auto!.GrupoAutomovel)
                .Include(a => a.Cliente)
                .Include(a => a.TaxasServicos)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Aluguel?> SelecionarAluguelCompletoPorIdAsync(Guid id)
        {
            return await SelecionarPorIdAsync(id);
        }

        public override async Task<List<Aluguel>> SelecionarTodosAsync()
        {
            return await dbContext.Alugueis
                .Include(a => a.Condutor)
                .Include(a => a.Automovel)
                .ThenInclude(auto => auto!.GrupoAutomovel)
                .Include(a => a.Cliente)
                .Include(a => a.TaxasServicos)
                .OrderByDescending(a => a.DataSaida)
                .ToListAsync();
        }

        public async Task<bool> ExisteAluguelAtivoParaAutomovelAsync(
            Guid automovelId,
            DateTimeOffset dataSaida,
            DateTimeOffset dataRetornoPrevisto,
            Guid? aluguelIdExcluir = null)
        {
            var query = dbContext.Alugueis
                .Where(a => a.AutomovelId == automovelId &&
                           a.Status != StatusAluguel.Cancelado &&
                           a.Status != StatusAluguel.Concluido &&
                           ((dataSaida >= a.DataSaida && dataSaida <= a.DataRetornoPrevisto) ||
                            (dataRetornoPrevisto >= a.DataSaida && dataRetornoPrevisto <= a.DataRetornoPrevisto) ||
                            (dataSaida <= a.DataSaida && dataRetornoPrevisto >= a.DataRetornoPrevisto)));

            if (aluguelIdExcluir.HasValue)
            {
                query = query.Where(a => a.Id != aluguelIdExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> VerificarDocumentosCondutorEmDiaAsync(Guid condutorId)
        {
            var condutor = await dbContext.Condutores
                .FirstOrDefaultAsync(c => c.Id == condutorId);

            if (condutor == null)
                return false;

            return condutor.ValidadeCnh > DateTimeOffset.Now;
        }

        public async Task<List<Aluguel>> SelecionarAlugueisAtivosPorAutomovelAsync(Guid automovelId)
        {
            return await dbContext.Alugueis
                .Where(a => a.AutomovelId == automovelId &&
                           (a.Status == StatusAluguel.Reservado || a.Status == StatusAluguel.EmAndamento))
                .ToListAsync();
        }

        public async Task<List<Aluguel>> SelecionarAlugueisPorStatusAsync(StatusAluguel status)
        {
            return await dbContext.Alugueis
                .Include(a => a.Condutor)
                .Include(a => a.Automovel)
                .Include(a => a.Cliente)
                .Where(a => a.Status == status)
                .OrderByDescending(a => a.DataSaida)
                .ToListAsync();
        }

        public async Task<List<Aluguel>> SelecionarAlugueisEmAbertoAsync()
        {
            return await dbContext.Alugueis
                .Include(a => a.Condutor)
                .Include(a => a.Automovel)
                .Include(a => a.Cliente)
                .Where(a => a.Status == StatusAluguel.Reservado || a.Status == StatusAluguel.EmAndamento)
                .OrderByDescending(a => a.DataSaida)
                .ToListAsync();
        }

        public Task<List<Aluguel>> SelecionarTodosAsync(int quantity)
        {
            throw new NotImplementedException();
        }
    }
}