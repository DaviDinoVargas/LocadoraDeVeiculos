using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloConfiguracao
{
    public class RepositorioConfiguracaoEmOrm : RepositorioBaseEmOrm<Configuracao>, IRepositorioConfiguracao
    {
        private readonly LocadoraDeVeiculosDbContext dbContext;

        public RepositorioConfiguracaoEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
        {
            this.dbContext = dbContext;
        }

        public async Task<Configuracao?> SelecionarPorEmpresaIdAsync(Guid empresaId)
        {
            return await dbContext.Configuracoes
                .FirstOrDefaultAsync(c => c.EmpresaId == empresaId && !c.Excluido);
        }

        public Task<List<Configuracao>> SelecionarTodosAsync(int quantity)
        {
            throw new NotImplementedException();
        }
    }
}