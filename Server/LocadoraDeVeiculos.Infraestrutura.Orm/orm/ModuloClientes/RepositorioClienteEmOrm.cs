using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente
{
    public class RepositorioClienteEmOrm : RepositorioBaseEmOrm<Cliente>, IRepositorioCliente
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioClienteEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public override async Task<Cliente?> SelecionarPorIdAsync(Guid id)
        {
            return await dbContext.Set<Cliente>()
                .Include("ClientePessoaJuridica")
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public override async Task<List<Cliente>> SelecionarTodosAsync()
        {
            return await dbContext.Set<Cliente>()
                .Include("ClientePessoaJuridica")
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }

        public async Task<bool> ExisteClienteComCpfAsync(string cpf, Guid? idExcluir = null)
        {
            var query = dbContext.Set<ClientePessoaFisica>()
                .Where(c => c.Cpf == cpf);

            if (idExcluir.HasValue)
            {
                query = query.Where(c => c.Id != idExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteClienteComCnpjAsync(string cnpj, Guid? idExcluir = null)
        {
            var query = dbContext.Set<ClientePessoaJuridica>()
                .Where(c => c.Cnpj == cnpj);

            if (idExcluir.HasValue)
            {
                query = query.Where(c => c.Id != idExcluir.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> ExisteAluguelEmAbertoAsync(Guid clienteId)
        {
            return await dbContext.Set<Aluguel>().AnyAsync(a =>
                a.ClienteId == clienteId &&
                (a.Status == StatusAluguel.Reservado || a.Status == StatusAluguel.EmAndamento));
        }

        public async Task<List<ClientePessoaFisica>> SelecionarPessoasFisicasAsync()
        {
            return await dbContext.Set<ClientePessoaFisica>()
                .Include("ClientePessoaJuridica")
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }

        public async Task<List<ClientePessoaJuridica>> SelecionarPessoasJuridicasAsync()
        {
            return await dbContext.Set<ClientePessoaJuridica>()
                .OrderBy(c => c.Nome)
                .ToListAsync();
        }

        public async Task<ClientePessoaFisica?> SelecionarPessoaFisicaPorIdAsync(Guid id)
        {
            return await dbContext.Set<ClientePessoaFisica>()
                .Include("ClientePessoaJuridica")
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<ClientePessoaJuridica?> SelecionarPessoaJuridicaPorIdAsync(Guid id)
        {
            return await dbContext.Set<ClientePessoaJuridica>()
                .FirstOrDefaultAsync(c => c.Id == id);
        }
    }
}