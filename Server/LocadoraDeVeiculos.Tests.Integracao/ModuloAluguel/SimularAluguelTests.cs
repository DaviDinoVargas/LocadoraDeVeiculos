using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Tests.Integracao.Compartilhado;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAluguel;

/// <summary>
/// O formulário de aluguel precisa mostrar o preço REAL (plano do grupo × dias + taxas) antes
/// de salvar. A simulação usa o mesmo CalculoAluguelService do cadastro, então os dois sempre
/// concordam, e não grava nada.
/// </summary>
[TestClass]
[TestCategory("Integração - Simulação de aluguel")]
public sealed class SimularAluguelTests
{
    private static SimularAluguelQueryHandler CriarHandler(CenarioAluguel c) => new(
        c.RepositorioAutomovel, c.RepositorioPlanoCobranca, c.RepositorioTaxaServico,
        c.RepositorioConfiguracao, c.TenantProvider,
        new SimularAluguelQueryValidator(),
        NullLogger<SimularAluguelQueryHandler>.Instance);

    private static readonly DateTimeOffset Saida = DateTimeOffset.UtcNow.AddDays(2);

    [TestMethod]
    public async Task Simular_SemTaxas_RetornaDiariasDoPlanoECaucaoPadrao()
    {
        await using var c = await CenarioAluguel.CriarAsync(precoDiaria: 100m);

        var resultado = await CriarHandler(c).Handle(
            new SimularAluguelQuery(c.Automovel.Id, Saida, Saida.AddDays(3), null), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, resultado.IsFailed ? string.Join("; ", resultado.Errors) : "");
        Assert.AreEqual(3, resultado.Value.Dias);
        Assert.AreEqual(100m, resultado.Value.PrecoDiaria);
        Assert.AreEqual(300m, resultado.Value.ValorDiarias);
        Assert.AreEqual(0m, resultado.Value.ValorTaxas);
        Assert.AreEqual(300m, resultado.Value.ValorPrevisto);
        Assert.AreEqual(Configuracao.ValorGarantiaPadrao, resultado.Value.ValorCaucao);
        Assert.AreEqual("Plano Padrão", resultado.Value.PlanoNome);
    }

    [TestMethod]
    public async Task Simular_ComTaxaDiariaEFixa_SomaComoOCadastro()
    {
        await using var c = await CenarioAluguel.CriarAsync(precoDiaria: 100m);
        var seguro = new TaxaServico("Seguro", 30m, TipoCalculo.Diario) { EmpresaId = c.EmpresaId };   // 30 * 3
        var limpeza = new TaxaServico("Limpeza", 50m, TipoCalculo.Fixo) { EmpresaId = c.EmpresaId };    // 50
        await c.RepositorioTaxaServico.CadastrarAsync(seguro);
        await c.RepositorioTaxaServico.CadastrarAsync(limpeza);
        await c.Contexto.SaveChangesAsync();

        var resultado = await CriarHandler(c).Handle(
            new SimularAluguelQuery(c.Automovel.Id, Saida, Saida.AddDays(3), new List<Guid> { seguro.Id, limpeza.Id }),
            CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.AreEqual(140m, resultado.Value.ValorTaxas);
        Assert.AreEqual(440m, resultado.Value.ValorPrevisto); // 300 + 90 + 50
    }

    [TestMethod]
    public async Task Simular_UsaACaucaoConfiguradaPelaEmpresa()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var config = new Configuracao(c.EmpresaId, 5.5m) { ValorGarantia = 2500m };
        await c.RepositorioConfiguracao.CadastrarAsync(config);
        await c.Contexto.SaveChangesAsync();

        var resultado = await CriarHandler(c).Handle(
            new SimularAluguelQuery(c.Automovel.Id, Saida, Saida.AddDays(1), null), CancellationToken.None);

        Assert.AreEqual(2500m, resultado.Value.ValorCaucao);
    }

    [TestMethod]
    public async Task Simular_BateComOValorGravadoPeloCadastro_ComValorPrevistoDoClienteIgnorado()
    {
        await using var c = await CenarioAluguel.CriarAsync(precoDiaria: 120m);
        var inicio = DateTimeOffset.UtcNow.AddDays(5);
        var fim = inicio.AddDays(4);

        var simulado = await CriarHandler(c).Handle(
            new SimularAluguelQuery(c.Automovel.Id, inicio, fim, null), CancellationToken.None);

        var criado = await c.CriarHandlerCadastro().Handle(
            new CadastrarAluguelCommand(c.Condutor.Id, c.Automovel.Id, c.Cliente.Id, inicio, fim,
                ValorPrevisto: 0m, TaxasServicosIds: new()),
            CancellationToken.None);

        Assert.IsTrue(criado.IsSuccess, criado.IsFailed ? string.Join("; ", criado.Errors) : "");
        var aluguel = await c.RepositorioAluguel.SelecionarAluguelCompletoPorIdAsync(criado.Value.Id);
        Assert.AreEqual(simulado.Value.ValorPrevisto, aluguel!.ValorPrevisto);
        Assert.AreEqual(480m, aluguel.ValorPrevisto);
    }

    [TestMethod]
    public async Task Simular_NaoGravaNenhumAluguel()
    {
        await using var c = await CenarioAluguel.CriarAsync();

        await CriarHandler(c).Handle(
            new SimularAluguelQuery(c.Automovel.Id, Saida, Saida.AddDays(2), null), CancellationToken.None);

        Assert.AreEqual(0, (await c.RepositorioAluguel.SelecionarTodosAsync()).Count);
    }

    [TestMethod]
    public async Task Simular_AutomovelInexistente_RetornaNaoEncontrado()
    {
        await using var c = await CenarioAluguel.CriarAsync();

        var resultado = await CriarHandler(c).Handle(
            new SimularAluguelQuery(Guid.NewGuid(), Saida, Saida.AddDays(2), null), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    [TestMethod]
    public async Task Simular_RetornoAntesDaSaida_EhRejeitado()
    {
        await using var c = await CenarioAluguel.CriarAsync();

        var resultado = await CriarHandler(c).Handle(
            new SimularAluguelQuery(c.Automovel.Id, Saida, Saida.AddDays(-1), null), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }
}
