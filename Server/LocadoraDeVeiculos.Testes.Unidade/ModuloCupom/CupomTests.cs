using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using System;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloCupom;

[TestClass]
public sealed class CupomTests
{
    private static Cupom CriarCupom(
        TipoDesconto tipo, decimal valor, DateTimeOffset validoAte, int? limiteUsos = null) =>
        new("PROMO10", "Cupom de teste", tipo, valor, validoAte, limiteUsos, parceiroId: null);

    [TestMethod]
    public void CalcularDesconto_Percentual_DeveAplicarPorcentagemSobreOValorBase()
    {
        var cupom = CriarCupom(TipoDesconto.Percentual, 10m, DateTimeOffset.UtcNow.AddDays(1));

        var desconto = cupom.CalcularDesconto(500m);

        Assert.AreEqual(50m, desconto);
    }

    [TestMethod]
    public void CalcularDesconto_ValorFixo_DeveDescontarOValorInformado()
    {
        var cupom = CriarCupom(TipoDesconto.ValorFixo, 80m, DateTimeOffset.UtcNow.AddDays(1));

        var desconto = cupom.CalcularDesconto(500m);

        Assert.AreEqual(80m, desconto);
    }

    [TestMethod]
    public void CalcularDesconto_ValorFixoMaiorQueOValorBase_NuncaDescontaMaisDoQueOTotal()
    {
        var cupom = CriarCupom(TipoDesconto.ValorFixo, 1000m, DateTimeOffset.UtcNow.AddDays(1));

        var desconto = cupom.CalcularDesconto(300m);

        Assert.AreEqual(300m, desconto);
    }

    [TestMethod]
    public void EstaValido_CupomExpirado_DeveRetornarFalso()
    {
        var cupom = CriarCupom(TipoDesconto.Percentual, 10m, DateTimeOffset.UtcNow.AddDays(-1));

        Assert.IsFalse(cupom.EstaValido(DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void EstaValido_CupomInativo_DeveRetornarFalso()
    {
        var cupom = CriarCupom(TipoDesconto.Percentual, 10m, DateTimeOffset.UtcNow.AddDays(1));
        cupom.Ativo = false;

        Assert.IsFalse(cupom.EstaValido(DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void EstaValido_LimiteDeUsosEsgotado_DeveRetornarFalso()
    {
        var cupom = CriarCupom(TipoDesconto.Percentual, 10m, DateTimeOffset.UtcNow.AddDays(1), limiteUsos: 2);

        cupom.RegistrarUso();
        cupom.RegistrarUso();

        Assert.IsFalse(cupom.EstaValido(DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void EstaValido_DentroDoLimiteDeUsos_DeveRetornarVerdadeiro()
    {
        var cupom = CriarCupom(TipoDesconto.Percentual, 10m, DateTimeOffset.UtcNow.AddDays(1), limiteUsos: 2);

        cupom.RegistrarUso();

        Assert.IsTrue(cupom.EstaValido(DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void EstaValido_SemLimiteDeUsos_NuncaEsgota()
    {
        var cupom = CriarCupom(TipoDesconto.Percentual, 10m, DateTimeOffset.UtcNow.AddDays(1), limiteUsos: null);

        for (var i = 0; i < 50; i++)
            cupom.RegistrarUso();

        Assert.IsTrue(cupom.EstaValido(DateTimeOffset.UtcNow));
    }
}
