using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
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
    public DbSet<AmostraFacial> AmostrasFaciais { get; set; }
    public DbSet<RegistroDeAuditoria> Auditoria { get; set; }
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
    public DbSet<Parceiro> Parceiros { get; set; }
    public DbSet<Cupom> Cupons { get; set; }
    public DbSet<DesafioCupom> DesafiosCupom { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // aplica todos os mapeamentos automaticamente (Mapeadores)
        var assembly = typeof(LocadoraDeVeiculosDbContext).Assembly;

        modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        base.OnModelCreating(modelBuilder);

        AplicarFiltrosMultiTenant(modelBuilder);

        // A trilha de auditoria não deriva de EntidadeBase, então o isolamento por empresa é aplicado aqui.
        modelBuilder.Entity<RegistroDeAuditoria>().HasQueryFilter(r =>
            r.EmpresaId != null && r.EmpresaId == tenant.EmpresaId);
    }

    // ---------------------------------------------------------------------------------------------
    // Trilha de auditoria (S14): toda criação/alteração/exclusão de entidade de negócio, mais o
    // cadastro e a remoção de amostras faciais, vira um RegistroDeAuditoria gravado na mesma
    // transação. Só nomes e identificadores; nunca valores.
    // ---------------------------------------------------------------------------------------------

    private static readonly HashSet<string> CamposQueNaoSaoAlteracao = ["CriadoEmUtc", "ExcluidoEmUtc", "Excluido"];

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RegistrarAuditoria();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RegistrarAuditoria();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static bool EhEntidadeAuditada(object entidade)
    {
        if (entidade is AmostraFacial)
            return true;

        for (var tipo = entidade.GetType().BaseType; tipo is not null; tipo = tipo.BaseType)
            if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(EntidadeBase<>))
                return true;

        return false;
    }

    private void RegistrarAuditoria()
    {
        ChangeTracker.DetectChanges();

        var entradas = ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                        && EhEntidadeAuditada(e.Entity))
            .ToList();

        foreach (var entrada in entradas)
        {
            var empresaDaEntidade = entrada.Properties
                .FirstOrDefault(p => p.Metadata.Name == "EmpresaId")?.CurrentValue as Guid?;
            var excluidoLogicamente = entrada.State == EntityState.Modified
                && entrada.Properties.Any(p => p.Metadata.Name == "Excluido" && p.IsModified && p.CurrentValue is true);

            var acao = entrada.State switch
            {
                EntityState.Added => "Criado",
                EntityState.Deleted => "Removido",
                _ => excluidoLogicamente ? "Excluído" : "Alterado"
            };

            Auditoria.Add(new RegistroDeAuditoria
            {
                EmpresaId = empresaDaEntidade is { } e && e != Guid.Empty ? e : tenant.EmpresaId,
                UsuarioId = tenant.UsuarioId,
                Acao = acao,
                Entidade = entrada.Entity.GetType().Name,
                EntidadeId = entrada.Properties.FirstOrDefault(p => p.Metadata.Name == "Id")?.CurrentValue as Guid?,
                CamposAlterados = acao == "Alterado"
                    ? string.Join(",", entrada.Properties
                        .Where(p => p.IsModified && !CamposQueNaoSaoAlteracao.Contains(p.Metadata.Name))
                        .Select(p => p.Metadata.Name))
                    : null,
                EnderecoIp = tenant.EnderecoIp
            });
        }
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
