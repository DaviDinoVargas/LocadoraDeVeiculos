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
        private readonly TimeProvider relogio;

        public RepositorioAluguelEmOrm(LocadoraDeVeiculosDbContext dbContext, TimeProvider? relogio = null) : base(dbContext)
        {
            this.dbContext = dbContext;
            this.relogio = relogio ?? TimeProvider.System;
        }

        public override async Task<Aluguel?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Alugueis
                .Include(a => a.Condutor)
                .Include(a => a.Automovel)
                .ThenInclude(auto => auto!.GrupoAutomovel)
                .Include(a => a.Cliente)
                .Include(a => a.TaxasServicos)
                .Include(a => a.Cupom)
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
            return await AlugueisAtivosNoPeriodo(dataSaida, dataRetornoPrevisto, aluguelIdExcluir)
                .AnyAsync(a => a.AutomovelId == automovelId);
        }

        public async Task<List<Guid>> SelecionarAutomoveisIndisponiveisAsync(
            DateTimeOffset dataSaida,
            DateTimeOffset dataRetornoPrevisto,
            Guid? aluguelIdExcluir = null)
        {
            return await AlugueisAtivosNoPeriodo(dataSaida, dataRetornoPrevisto, aluguelIdExcluir)
                .Select(a => a.AutomovelId)
                .Distinct()
                .ToListAsync();
        }

        // Única definição de "aluguel que bloqueia o automóvel": não cancelado, não concluído e com
        // período que cruza [dataSaida, dataRetornoPrevisto] (extremos incluídos). Cadastro/edição
        // (409) e a listagem de disponíveis usam a mesma regra, então nunca discordam.
        // Aluguel EM ANDAMENTO com o retorno previsto já vencido (atrasado) continua bloqueando o
        // veículo até a devolução ser registrada: o carro não voltou, então não dá para alugá-lo de
        // novo. Antes só o cruzamento de datas contava e o veículo atrasado aparecia como livre.
        private IQueryable<Aluguel> AlugueisAtivosNoPeriodo(
            DateTimeOffset dataSaida,
            DateTimeOffset dataRetornoPrevisto,
            Guid? aluguelIdExcluir)
        {
            var agora = relogio.GetUtcNow();

            var query = dbContext.Alugueis
                .Where(a => a.Status != StatusAluguel.Cancelado &&
                            a.Status != StatusAluguel.Concluido &&
                            a.DataSaida <= dataRetornoPrevisto &&
                            (a.DataRetornoPrevisto >= dataSaida ||
                             (a.Status == StatusAluguel.EmAndamento && a.DataRetornoPrevisto < agora)));

            if (aluguelIdExcluir.HasValue)
            {
                query = query.Where(a => a.Id != aluguelIdExcluir.Value);
            }

            return query;
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
    }
}