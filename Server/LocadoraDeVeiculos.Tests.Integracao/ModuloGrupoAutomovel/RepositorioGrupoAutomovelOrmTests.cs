using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloGrupoAutomovel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloGrupoAutomovel;

/// <summary>
/// Estas checagens ficavam comentadas nos handlers de exclusão (o método nem existia na
/// interface do repositório, então nunca teriam compilado se fossem reativadas como estavam).
/// </summary>
[TestClass]
[TestCategory("Integração - GrupoAutomovel")]
public sealed class RepositorioGrupoAutomovelOrmTests
{
    private sealed class TenantProviderFake : ITenantProvider
    {
        public Guid? EmpresaId { get; init; }
        public bool EstaNoCargo(string cargo) => false;
    }

    // As checagens de "vinculado" abaixo consultam Automovel/PlanoCobranca, que agora também
    // são filtrados por tenant — por isso o contexto de teste precisa de um tenant real
    // (correspondendo ao EmpresaId dos registros), exatamente como aconteceria em produção.
    private static LocadoraDeVeiculosDbContext CriarContexto(Guid empresaId)
    {
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LocadoraDeVeiculosDbContext(options, new TenantProviderFake { EmpresaId = empresaId });
    }

    [TestMethod]
    public async Task ExisteAutomovelVinculadoAsync_ComAutomovelNoGrupo_DeveRetornarTrue()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(empresaId);
        var repositorioGrupo = new RepositorioGrupoAutomovelEmOrm(contexto);
        var repositorioAutomovel = new RepositorioAutomovelEmOrm(contexto);

        var grupo = new GrupoAutomovel("Grupo Popular") { EmpresaId = empresaId };
        await repositorioGrupo.CadastrarAsync(grupo);

        var automovel = new Automovel("ABC1234", "Fiat", "Prata", "Uno",
            TipoCombustivel.Gasolina, 50, 2020, null, grupo.Id) { EmpresaId = grupo.EmpresaId };
        await repositorioAutomovel.CadastrarAsync(automovel);

        await contexto.SaveChangesAsync();

        Assert.IsTrue(await repositorioGrupo.ExisteAutomovelVinculadoAsync(grupo.Id));
    }

    [TestMethod]
    public async Task ExisteAutomovelVinculadoAsync_SemAutomovelNoGrupo_DeveRetornarFalse()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(empresaId);
        var repositorioGrupo = new RepositorioGrupoAutomovelEmOrm(contexto);

        var grupo = new GrupoAutomovel("Grupo Vazio") { EmpresaId = empresaId };
        await repositorioGrupo.CadastrarAsync(grupo);
        await contexto.SaveChangesAsync();

        Assert.IsFalse(await repositorioGrupo.ExisteAutomovelVinculadoAsync(grupo.Id));
    }

    [TestMethod]
    public async Task ExistePlanoCobrancaVinculadoAsync_ComPlanoNoGrupo_DeveRetornarTrue()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(empresaId);
        var repositorioGrupo = new RepositorioGrupoAutomovelEmOrm(contexto);

        var grupo = new GrupoAutomovel("Grupo Executivo") { EmpresaId = empresaId };
        await repositorioGrupo.CadastrarAsync(grupo);

        var plano = new PlanoCobranca(grupo.Id, empresaId, "Plano Padrão", 150m, 0.8m, 100);
        contexto.Set<PlanoCobranca>().Add(plano);

        await contexto.SaveChangesAsync();

        Assert.IsTrue(await repositorioGrupo.ExistePlanoCobrancaVinculadoAsync(grupo.Id));
    }

    [TestMethod]
    public async Task ExisteGrupoComNomeAsync_ExcluindoOProprioId_NaoDeveAcusarConflitoConsigoMesmo()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(empresaId);
        var repositorioGrupo = new RepositorioGrupoAutomovelEmOrm(contexto);

        var grupo = new GrupoAutomovel("Grupo SUV") { EmpresaId = empresaId };
        await repositorioGrupo.CadastrarAsync(grupo);
        await contexto.SaveChangesAsync();

        var conflitaComOutro = await repositorioGrupo.ExisteGrupoComNomeAsync("Grupo SUV");
        var conflitaConsigoMesmo = await repositorioGrupo.ExisteGrupoComNomeAsync("Grupo SUV", grupo.Id);

        Assert.IsTrue(conflitaComOutro);
        Assert.IsFalse(conflitaConsigoMesmo);
    }
}
