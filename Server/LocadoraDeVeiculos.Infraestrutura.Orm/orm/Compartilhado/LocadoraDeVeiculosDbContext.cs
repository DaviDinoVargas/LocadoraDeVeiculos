using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;

public class LocadoraDeVeiculosDbContext(
    DbContextOptions options,
    ITenantProvider? tenantProvider = null
) : IdentityDbContext<Usuario, Cargo, Guid>(options), IContextoPersistencia
{
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Funcionario> Funcionarios { get; set; }
    public DbSet<GrupoAutomovel> GruposAutomovel { get; set; }
    public DbSet<PlanoCobranca> PlanoCobranca { get; set; }
    public DbSet<Automovel> Automoveis { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Condutor> Condutores { get; set; }
    public DbSet<TaxaServico> TaxasServico { get; set; }
    public DbSet<Aluguel> Alugueis { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Se tiver filtros multi-tenant no futuro, colocar aqui
        if (tenantProvider is not null)
        {
            // Exemplo:
            modelBuilder.Entity<Funcionario>()
                 .HasQueryFilter(f => f.EmpresaId == tenantProvider.EmpresaId && !f.Excluido);
        }

        // aplica todos os mapeamentos automaticamente (Mapeadores)
        var assembly = typeof(LocadoraDeVeiculosDbContext).Assembly;

        modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> GravarAsync()
    {
        return await SaveChangesAsync();
    }

    public async Task RollbackAsync()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }

        await Task.CompletedTask;
    }
}
