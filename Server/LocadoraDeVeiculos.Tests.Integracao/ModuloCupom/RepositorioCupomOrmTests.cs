using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCupom;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloCupom;

[TestClass]
[TestCategory("Integração - Cupom")]
public sealed class RepositorioCupomOrmTests
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
    public async Task ExisteCupomComCodigoAsync_EhCaseInsensitive()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorio = new RepositorioCupomEmOrm(contexto);

        var cupom = new Cupom("promo10", "Desconto de lançamento", TipoDesconto.Percentual, 10m,
            DateTimeOffset.UtcNow.AddDays(30), limiteUsos: null, parceiroId: null)
        {
            EmpresaId = empresaId
        };

        await repositorio.CadastrarAsync(cupom);
        await contexto.SaveChangesAsync();

        Assert.IsTrue(await repositorio.ExisteCupomComCodigoAsync("PROMO10"));
        Assert.IsTrue(await repositorio.ExisteCupomComCodigoAsync("promo10"));
    }

    [TestMethod]
    public async Task SelecionarPorCodigoAsync_DeveEncontrarIndependenteDeCaixaAlta()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorio = new RepositorioCupomEmOrm(contexto);

        var cupom = new Cupom("verao2026", "Cupom de verão", TipoDesconto.ValorFixo, 50m,
            DateTimeOffset.UtcNow.AddDays(10), limiteUsos: 100, parceiroId: null)
        {
            EmpresaId = empresaId
        };

        await repositorio.CadastrarAsync(cupom);
        await contexto.SaveChangesAsync();

        var encontrado = await repositorio.SelecionarPorCodigoAsync("VERAO2026");

        Assert.IsNotNull(encontrado);
        Assert.AreEqual(cupom.Id, encontrado!.Id);
    }

    [TestMethod]
    public async Task EmpresaB_NaoDeveEnxergarCupomDaEmpresaA()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var empresaA = Guid.NewGuid();
        var empresaB = Guid.NewGuid();

        await using (var contextoA = CriarContexto(nomeBanco, empresaA))
        {
            var cupom = new Cupom("SOEMPRESAA", "Cupom exclusivo", TipoDesconto.Percentual, 15m,
                DateTimeOffset.UtcNow.AddDays(5), limiteUsos: null, parceiroId: null)
            {
                EmpresaId = empresaA
            };

            contextoA.Set<Cupom>().Add(cupom);
            await contextoA.SaveChangesAsync();
        }

        await using var contextoB = CriarContexto(nomeBanco, empresaB);
        var repositorioB = new RepositorioCupomEmOrm(contextoB);

        Assert.IsNull(await repositorioB.SelecionarPorCodigoAsync("SOEMPRESAA"));
    }
}
