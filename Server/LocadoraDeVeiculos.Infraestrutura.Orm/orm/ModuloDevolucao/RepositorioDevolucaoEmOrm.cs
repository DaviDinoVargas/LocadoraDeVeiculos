using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDevolucao
{
    public class RepositorioDevolucaoEmOrm : RepositorioBaseEmOrm<Devolucao>, IRepositorioDevolucao
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioDevolucaoEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        // A lista também precisa do aluguel (condutor, automóvel e cliente): sem estes Include a tabela de devoluções saía com
        // "N/A" em todas as colunas, porque a classe base só carrega a própria devolução.
        public override async Task<List<Devolucao>> SelecionarTodosAsync()
        {
            return await dbContext.Devolucoes
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Condutor)
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Automovel)
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Cliente)
                .ToListAsync();
        }

        public override async Task<Devolucao?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Devolucoes
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Condutor)
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Automovel)
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Cliente)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<bool> ExisteDevolucaoParaAluguelAsync(Guid aluguelId)
        {
            return await dbContext.Devolucoes
                .AnyAsync(d => d.AluguelId == aluguelId && !d.Excluido);
        }

        public async Task<Devolucao?> SelecionarPorAluguelIdAsync(Guid aluguelId)
        {
            return await dbContext.Devolucoes
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Condutor)
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Automovel)
                .Include(d => d.Aluguel)
                .ThenInclude(a => a!.Cliente)
                .FirstOrDefaultAsync(d => d.AluguelId == aluguelId);
        }
    }
}