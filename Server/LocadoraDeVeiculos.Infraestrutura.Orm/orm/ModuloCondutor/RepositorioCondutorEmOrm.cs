using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCondutor
{
    public class RepositorioCondutorEmOrm : RepositorioBaseEmOrm<Condutor>, IRepositorioCondutor
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioCondutorEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<Condutor?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Condutores
                .Include(c => c.Cliente)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public override async Task<List<Condutor>> SelecionarTodosAsync()
        {
            return await dbContext.Condutores
                .Include(c => c.Cliente)
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }

        public async Task<bool> ExisteCondutorComCpfAsync(string cpf, Guid? idExcluir = null)
        {
            var query = dbContext.Condutores
                .Where(c => c.Cpf == cpf);

            if (idExcluir.HasValue)
            {
                query = query.Where(c => c.Id != idExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteCondutorComCnhAsync(string cnh, Guid? idExcluir = null)
        {
            var query = dbContext.Condutores
                .Where(c => c.Cnh == cnh);

            if (idExcluir.HasValue)
            {
                query = query.Where(c => c.Id != idExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteAluguelEmAbertoAsync(Guid condutorId)
        {
            // Esta verificação será implementada quando tivermos o módulo de Aluguéis
            return await Task.FromResult(false);
        }

        public async Task<List<Condutor>> SelecionarPorClienteAsync(Guid clienteId)
        {
            return await dbContext.Condutores
                .Include(c => c.Cliente)
                .Where(c => c.ClienteId == clienteId)
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }

        public Task<List<Condutor>> SelecionarTodosAsync(int quantity)
        {
            throw new NotImplementedException();
        }
    }
}