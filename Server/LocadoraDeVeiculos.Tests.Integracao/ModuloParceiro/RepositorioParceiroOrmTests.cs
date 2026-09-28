using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloParceiro;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloParceiro;

[TestClass]
[TestCategory("Integração - Parceiro")]
public sealed class RepositorioParceiroOrmTests
{
    private sealed class TenantProviderFake : ITenantProvider
    {
        public Guid? EmpresaId { get; init; }
        public bool EstaNoCargo(string cargo) => false;
    }

    private static LocadoraDeVeiculosDbContext CriarContexto(string nomeBanco, Guid empresaId)
    {
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;

        return new LocadoraDeVeiculosDbContext(options, new TenantProviderFake { EmpresaId = empresaId });
    }

    [TestMethod]
    public async Task ExisteParceiroComCnpjAsync_DetectaDuplicata()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorio = new RepositorioParceiroEmOrm(contexto);

        var parceiro = new Parceiro("Posto Central", "12.345.678/0001-90", CategoriaParceiro.PostoDeCombustivel)
        {
            EmpresaId = empresaId
        };

        await repositorio.CadastrarAsync(parceiro);
        await contexto.SaveChangesAsync();

        Assert.IsTrue(await repositorio.ExisteParceiroComCnpjAsync("12.345.678/0001-90"));
        Assert.IsFalse(await repositorio.ExisteParceiroComCnpjAsync("99.999.999/0001-99"));
    }

    [TestMethod]
    public async Task ExisteParceiroComCnpjAsync_ComIdExcluido_IgnoraOProprioRegistro()
    {
        // Cenário real: editar um parceiro sem mudar o CNPJ não pode acusar
        // "duplicado" contra ele mesmo.
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorio = new RepositorioParceiroEmOrm(contexto);

        var parceiro = new Parceiro("Hotel Vista", "11.222.333/0001-44", CategoriaParceiro.Hotel)
        {
            EmpresaId = empresaId
        };

        await repositorio.CadastrarAsync(parceiro);
        await contexto.SaveChangesAsync();

        var existeIgnorandoAMesmaEntidade = await repositorio.ExisteParceiroComCnpjAsync("11.222.333/0001-44", parceiro.Id);
        var existeSemIgnorar = await repositorio.ExisteParceiroComCnpjAsync("11.222.333/0001-44");

        Assert.IsFalse(existeIgnorandoAMesmaEntidade);
        Assert.IsTrue(existeSemIgnorar);
    }

    [TestMethod]
    public async Task ExisteCupomVinculadoAsync_QuandoHaCupomComEsseParceiro_RetornaTrue()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorioParceiro = new RepositorioParceiroEmOrm(contexto);

        var parceiro = new Parceiro("Seguradora Alfa", "22.333.444/0001-55", CategoriaParceiro.Seguradora)
        {
            EmpresaId = empresaId
        };
        await repositorioParceiro.CadastrarAsync(parceiro);
        await contexto.SaveChangesAsync();

        var cupom = new Cupom("PARCERIA10", "Desconto de parceria", TipoDesconto.Percentual, 10m,
            DateTimeOffset.UtcNow.AddDays(30), limiteUsos: null, parceiroId: parceiro.Id)
        {
            EmpresaId = empresaId
        };
        contexto.Set<Cupom>().Add(cupom);
        await contexto.SaveChangesAsync();

        Assert.IsTrue(await repositorioParceiro.ExisteCupomVinculadoAsync(parceiro.Id));
    }

    [TestMethod]
    public async Task ExisteCupomVinculadoAsync_SemCupons_RetornaFalse()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorioParceiro = new RepositorioParceiroEmOrm(contexto);

        var parceiro = new Parceiro("Oficina Rápida", "33.444.555/0001-66", CategoriaParceiro.Oficina)
        {
            EmpresaId = empresaId
        };
        await repositorioParceiro.CadastrarAsync(parceiro);
        await contexto.SaveChangesAsync();

        Assert.IsFalse(await repositorioParceiro.ExisteCupomVinculadoAsync(parceiro.Id));
    }

    [TestMethod]
    public async Task EmpresaB_NaoDeveEnxergarParceiroDaEmpresaA()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var empresaA = Guid.NewGuid();
        var empresaB = Guid.NewGuid();

        await using (var contextoA = CriarContexto(nomeBanco, empresaA))
        {
            var parceiro = new Parceiro("Restaurante da Empresa A", "44.555.666/0001-77", CategoriaParceiro.Restaurante)
            {
                EmpresaId = empresaA
            };
            contextoA.Set<Parceiro>().Add(parceiro);
            await contextoA.SaveChangesAsync();
        }

        await using var contextoB = CriarContexto(nomeBanco, empresaB);
        var repositorioB = new RepositorioParceiroEmOrm(contextoB);

        var registros = await repositorioB.SelecionarTodosAsync();

        Assert.AreEqual(0, registros.Count);
    }
}
