using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;

public class LocadoraDeVeiculosDbContext(
    DbContextOptions options,
    ITenantProvider? tenantProvider = null
) : IdentityDbContext<Usuario, Cargo, Guid>(options), IContextoPersistencia
{
    // Null object pattern: garante que "tenant" NUNCA seja null. Isso importa porque o EF Core
    // avalia acessos de membro dentro de HasQueryFilter de forma antecipada, fora da ordem de
    // curto-circuito do C# — "tenantProvider == null || e.EmpresaId == tenantProvider.EmpresaId"
    // lança NullReferenceException quando tenantProvider é null, mesmo com o "||" na frente.
    private readonly ITenantProvider tenant = tenantProvider ?? SemTenant.Instancia;

    private sealed class SemTenant : ITenantProvider
    {
        public static readonly SemTenant Instancia = new();
        public Guid? EmpresaId => null;
        public bool EstaNoCargo(string cargo) => false;
    }

    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<Funcionario> Funcionarios { get; set; }
    public DbSet<GrupoAutomovel> GruposAutomovel { get; set; }
    public DbSet<PlanoCobranca> PlanoCobranca { get; set; }
    public DbSet<Automovel> Automoveis { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Condutor> Condutores { get; set; }
    public DbSet<TaxaServico> TaxasServico { get; set; }
    public DbSet<Aluguel> Alugueis { get; set; }
    public DbSet<Devolucao> Devolucoes { get; set; }
    public DbSet<Configuracao> Configuracoes { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // aplica todos os mapeamentos automaticamente (Mapeadores)
        var assembly = typeof(LocadoraDeVeiculosDbContext).Assembly;

        modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        base.OnModelCreating(modelBuilder);

        AplicarFiltrosMultiTenant(modelBuilder);
    }

    /// <summary>
    /// Aplica automaticamente o isolamento multi-tenant (EmpresaId) e o filtro de exclusão lógica
    /// a TODA entidade que derive de EntidadeBase&lt;T&gt;, sem depender de configurar cada uma manualmente.
    /// Isso evita que uma nova entidade "esqueça" o filtro, como acontecia antes (só Funcionario tinha).
    /// </summary>
    private void AplicarFiltrosMultiTenant(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Em TPH (ex.: ClientePessoaFisica/Juridica), só a raiz da hierarquia precisa do filtro.
            if (entityType.BaseType is not null)
                continue;

            var clrType = entityType.ClrType;
            var baseClr = clrType.BaseType;

            if (baseClr is not { IsGenericType: true } || baseClr.GetGenericTypeDefinition() != typeof(EntidadeBase<>))
                continue;

            AplicarFiltroTenantMethod.MakeGenericMethod(clrType).Invoke(this, new object[] { modelBuilder });
        }
    }

    private static readonly MethodInfo AplicarFiltroTenantMethod = typeof(LocadoraDeVeiculosDbContext)
        .GetMethod(nameof(AplicarFiltroTenant), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void AplicarFiltroTenant<TEntidade>(ModelBuilder modelBuilder)
        where TEntidade : EntidadeBase<TEntidade>
    {
        modelBuilder.Entity<TEntidade>().HasQueryFilter(e =>
            !e.Excluido && e.EmpresaId == tenant.EmpresaId);
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
