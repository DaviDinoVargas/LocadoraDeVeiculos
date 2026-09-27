using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using System;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloDevolucao;

/// <summary>
/// Regressão do bug: a multa por atraso era sempre 10% fixo do valor do aluguel, não importava
/// se o atraso era de 1 dia ou de 30. Agora precisa ser proporcional aos dias de atraso reais.
/// </summary>
[TestClass]
public sealed class DevolucaoTests
{
    private static Devolucao CriarDevolucao(DateTimeOffset dataDevolucao) =>
        new(Guid.NewGuid(), dataDevolucao, quilometragemFinal: 100, combustivelNoTanque: 50, NivelCombustivel.Cheio);

    [TestMethod]
    public void CalcularValores_SemAtraso_NaoDeveCobrarMulta()
    {
        var dataSaida = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var dataRetornoPrevisto = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero); // 4 dias
        var devolucao = CriarDevolucao(dataRetornoPrevisto); // devolveu na hora certa

        devolucao.CalcularValores(
            precoCombustivel: 5m, capacidadeTanque: 50, quilometragemInicial: 0,
            dataSaida, dataRetornoPrevisto, valorPrevistoAluguel: 400m);

        Assert.AreEqual(0m, devolucao.ValorMultas);
    }

    [TestMethod]
    public void CalcularValores_UmDiaDeAtraso_DeveCobrarUmaFracaoDaDiaria()
    {
        var dataSaida = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var dataRetornoPrevisto = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero); // 4 dias contratados
        var dataDevolucaoReal = dataRetornoPrevisto.AddDays(1); // 1 dia de atraso
        var devolucao = CriarDevolucao(dataDevolucaoReal);

        devolucao.CalcularValores(
            precoCombustivel: 5m, capacidadeTanque: 50, quilometragemInicial: 0,
            dataSaida, dataRetornoPrevisto, valorPrevistoAluguel: 400m);

        // diária = 400 / 4 dias = 100; multa = 100 * 10% * 1 dia de atraso = 10
        Assert.AreEqual(10m, devolucao.ValorMultas);
    }

    [TestMethod]
    public void CalcularValores_CincoDiasDeAtraso_DeveCobrarCincoVezesMaisQueUmDia()
    {
        var dataSaida = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var dataRetornoPrevisto = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero); // 4 dias contratados

        var devolucaoUmDia = CriarDevolucao(dataRetornoPrevisto.AddDays(1));
        devolucaoUmDia.CalcularValores(5m, 50, 0, dataSaida, dataRetornoPrevisto, 400m);

        var devolucaoCincoDias = CriarDevolucao(dataRetornoPrevisto.AddDays(5));
        devolucaoCincoDias.CalcularValores(5m, 50, 0, dataSaida, dataRetornoPrevisto, 400m);

        // Esta é exatamente a regressão do bug original: antes, os dois casos davam o mesmo
        // valor fixo (10% do aluguel inteiro), independente de quantos dias de atraso houvesse.
        Assert.AreNotEqual(devolucaoUmDia.ValorMultas, devolucaoCincoDias.ValorMultas);
        Assert.AreEqual(devolucaoUmDia.ValorMultas * 5, devolucaoCincoDias.ValorMultas);
        Assert.AreEqual(50m, devolucaoCincoDias.ValorMultas);
    }

    [TestMethod]
    public void CalcularValores_ValorTotal_DeveSomarAluguelMultaECombustivel()
    {
        var dataSaida = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var dataRetornoPrevisto = new DateTimeOffset(2026, 1, 3, 9, 0, 0, TimeSpan.Zero); // 2 dias

        var devolucao = new Devolucao(Guid.NewGuid(), dataRetornoPrevisto, quilometragemFinal: 100,
            combustivelNoTanque: 0, NivelCombustivel.Cheio); // tanque vazio, nível esperado "Cheio"

        devolucao.CalcularValores(
            precoCombustivel: 5m, capacidadeTanque: 50, quilometragemInicial: 0,
            dataSaida, dataRetornoPrevisto, valorPrevistoAluguel: 200m);

        // sem atraso (multa 0) + combustível: faltam 50L cheios * 5 = 250
        Assert.AreEqual(0m, devolucao.ValorMultas);
        Assert.AreEqual(250m, devolucao.ValorAdicionalCombustivel);
        Assert.AreEqual(450m, devolucao.ValorTotal); // 200 + 0 + 250
    }
}
