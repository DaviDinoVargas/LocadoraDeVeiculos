using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Servicos;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAluguel;

/// <summary>
/// O valor previsto de um aluguel nunca deve confiar no que o cliente manda: precisa ser
/// recalculado no servidor a partir do plano de cobrança (diária) e das taxas selecionadas.
/// </summary>
[TestClass]
public sealed class CalculoAluguelServiceTests
{
    private static PlanoCobranca CriarPlano(decimal precoDiaria) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Plano Teste", precoDiaria, precoPorKm: 0.50m, kmLivreLimite: 100);

    [TestMethod]
    public void CalcularDiasContratados_MesmoDia_DeveContarComoUmaDiaria()
    {
        var saida = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var retorno = new DateTimeOffset(2026, 1, 10, 18, 0, 0, TimeSpan.Zero);

        var dias = CalculoAluguelService.CalcularDiasContratados(saida, retorno);

        Assert.AreEqual(1, dias);
    }

    [TestMethod]
    public void CalcularDiasContratados_TresDiasCompletos_DeveContarTres()
    {
        var saida = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var retorno = new DateTimeOffset(2026, 1, 13, 9, 0, 0, TimeSpan.Zero);

        var dias = CalculoAluguelService.CalcularDiasContratados(saida, retorno);

        Assert.AreEqual(3, dias);
    }

    [TestMethod]
    public void CalcularValorPrevisto_SemTaxas_DeveSerSomenteDiariaVezesDias()
    {
        var plano = CriarPlano(precoDiaria: 100m);
        var saida = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var retorno = new DateTimeOffset(2026, 1, 13, 9, 0, 0, TimeSpan.Zero); // 3 dias

        var valor = CalculoAluguelService.CalcularValorPrevisto(plano, saida, retorno, new List<TaxaServico>());

        Assert.AreEqual(300m, valor);
    }

    [TestMethod]
    public void CalcularValorPrevisto_ComTaxaFixaETaxaDiaria_DeveSomarCorretamente()
    {
        var plano = CriarPlano(precoDiaria: 100m);
        var saida = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
        var retorno = new DateTimeOffset(2026, 1, 13, 9, 0, 0, TimeSpan.Zero); // 3 dias

        var taxas = new List<TaxaServico>
        {
            new("Seguro", 30m, TipoCalculo.Diario),  // 30 * 3 dias = 90
            new("Limpeza", 50m, TipoCalculo.Fixo)     // 50 (independe dos dias)
        };

        var valor = CalculoAluguelService.CalcularValorPrevisto(plano, saida, retorno, taxas);

        // 100 * 3 (diárias) + 90 (seguro diário) + 50 (limpeza fixa) = 440
        Assert.AreEqual(440m, valor);
    }

    [TestMethod]
    public void CalcularValorPrevisto_IgnoraQualquerValorQueNaoVenhaDoPlanoOuDasTaxas()
    {
        // Não existe parâmetro de "valor enviado pelo cliente" nesta função de propósito:
        // o cálculo só pode depender do plano de cobrança e das taxas, nunca de input externo.
        var plano = CriarPlano(precoDiaria: 200m);
        var saida = DateTimeOffset.UtcNow;
        var retorno = saida.AddDays(2);

        var valor1 = CalculoAluguelService.CalcularValorPrevisto(plano, saida, retorno, new List<TaxaServico>());
        var valor2 = CalculoAluguelService.CalcularValorPrevisto(plano, saida, retorno, new List<TaxaServico>());

        Assert.AreEqual(valor1, valor2);
        Assert.AreEqual(400m, valor1);
    }
}
