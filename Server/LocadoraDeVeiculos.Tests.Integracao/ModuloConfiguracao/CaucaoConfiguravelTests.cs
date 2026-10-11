using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Tests.Integracao.Compartilhado;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloConfiguracao;

/// <summary>
/// A caução do aluguel vinha fixa em 1000 no domínio e no front, e Configuracao.ValorGarantia
/// não podia ser editada. Agora a empresa define o valor e cada NOVO aluguel o copia.
/// </summary>
[TestClass]
[TestCategory("Integração - Caução configurável")]
public sealed class CaucaoConfiguravelTests
{
    private static ConfigurarValorGarantiaCommandHandler CriarHandlerGarantia(CenarioAluguel c) => new(
        c.Contexto, c.RepositorioConfiguracao, c.TenantProvider,
        new ConfigurarValorGarantiaCommandValidator(),
        NullLogger<ConfigurarValorGarantiaCommandHandler>.Instance);

    private static CadastrarAluguelCommand NovoAluguel(CenarioAluguel c, int diasAFrente = 1) => new(
        c.Condutor.Id, c.Automovel.Id, c.Cliente.Id,
        DateTimeOffset.UtcNow.AddDays(diasAFrente), DateTimeOffset.UtcNow.AddDays(diasAFrente + 2),
        ValorPrevisto: 1m, // o validator ainda exige > 0; o servidor ignora e calcula (ver A3)
        TaxasServicosIds: new());

    [TestMethod]
    public async Task ConfigurarValorGarantia_SemConfiguracaoGravada_CriaComPrecoPadraoDoCombustivel()
    {
        await using var c = await CenarioAluguel.CriarAsync();

        var resultado = await CriarHandlerGarantia(c).Handle(new ConfigurarValorGarantiaCommand(2500m), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.AreEqual(2500m, resultado.Value.ValorGarantia);
        Assert.AreEqual(Configuracao.PrecoCombustivelPadrao, resultado.Value.PrecoCombustivel);
    }

    [TestMethod]
    public async Task ConfigurarValorGarantia_ComConfiguracaoExistente_MantemOPrecoDoCombustivel()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await c.RepositorioConfiguracao.CadastrarAsync(new Configuracao(c.EmpresaId, 7.25m));
        await c.Contexto.SaveChangesAsync();

        var resultado = await CriarHandlerGarantia(c).Handle(new ConfigurarValorGarantiaCommand(1800m), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.AreEqual(1800m, resultado.Value.ValorGarantia);
        Assert.AreEqual(7.25m, resultado.Value.PrecoCombustivel);
    }

    [TestMethod]
    public async Task ConfigurarValorGarantia_Negativo_EhRejeitado()
    {
        await using var c = await CenarioAluguel.CriarAsync();

        var resultado = await CriarHandlerGarantia(c).Handle(new ConfigurarValorGarantiaCommand(-1m), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    [TestMethod]
    public async Task CadastrarAluguel_SemConfiguracao_UsaCaucaoPadrao()
    {
        await using var c = await CenarioAluguel.CriarAsync();

        var criado = await c.CriarHandlerCadastro().Handle(NovoAluguel(c), CancellationToken.None);

        Assert.IsTrue(criado.IsSuccess);
        var aluguel = await c.RepositorioAluguel.SelecionarAluguelCompletoPorIdAsync(criado.Value.Id);
        Assert.AreEqual(Configuracao.ValorGarantiaPadrao, aluguel!.ValorCaucao);
    }

    [TestMethod]
    public async Task CadastrarAluguel_ComGarantiaConfigurada_CopiaOValorConfigurado()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await CriarHandlerGarantia(c).Handle(new ConfigurarValorGarantiaCommand(2500m), CancellationToken.None);

        var criado = await c.CriarHandlerCadastro().Handle(NovoAluguel(c), CancellationToken.None);

        Assert.IsTrue(criado.IsSuccess, criado.IsFailed ? string.Join("; ", criado.Errors) : "");
        var aluguel = await c.RepositorioAluguel.SelecionarAluguelCompletoPorIdAsync(criado.Value.Id);
        Assert.AreEqual(2500m, aluguel!.ValorCaucao);
    }

    [TestMethod]
    public async Task AlterarGarantiaDepois_NaoMudaACaucaoDeAlugueisJaContratados()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await CriarHandlerGarantia(c).Handle(new ConfigurarValorGarantiaCommand(2500m), CancellationToken.None);
        var primeiro = await c.CriarHandlerCadastro().Handle(NovoAluguel(c), CancellationToken.None);

        await CriarHandlerGarantia(c).Handle(new ConfigurarValorGarantiaCommand(900m), CancellationToken.None);

        var aluguel = await c.RepositorioAluguel.SelecionarAluguelCompletoPorIdAsync(primeiro.Value.Id);
        Assert.AreEqual(2500m, aluguel!.ValorCaucao);
    }
}
