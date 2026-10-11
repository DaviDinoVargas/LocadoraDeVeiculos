using LocadoraDeVeiculos.Core.Aplicacao.ModuloDashboard.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDashboard.Handlers;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Tests.Integracao.Compartilhado;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloDashboard;

/// <summary>
/// A Home era só uma grade de atalhos. O resumo (frota, aluguéis, faturamento e últimos meses) é
/// calculado no servidor, com um relógio fixo nos testes para "atrasado" e "do mês" serem determinísticos.
/// </summary>
[TestClass]
[TestCategory("Integração - Resumo do dashboard")]
public sealed class ResumoDashboardTests
{
    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    // 15/10/2026 12:00 UTC
    private static readonly DateTimeOffset Agora = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset Dia(int mes, int dia) => new(2026, mes, dia, 9, 0, 0, TimeSpan.Zero);

    private static ObterResumoDashboardQueryHandler CriarHandler(CenarioAluguel c) =>
        new(c.Contexto, new RelogioFixo(Agora), NullLogger<ObterResumoDashboardQueryHandler>.Instance);

    private static Aluguel NovoAluguel(CenarioAluguel c, Automovel automovel, DateTimeOffset saida, DateTimeOffset retorno, StatusAluguel status)
    {
        var aluguel = new Aluguel(c.Condutor.Id, automovel.Id, c.Cliente.Id, saida, retorno, 300m) { EmpresaId = c.EmpresaId };
        aluguel.Status = status;
        c.Contexto.Set<Aluguel>().Add(aluguel);
        return aluguel;
    }

    private static void NovaDevolucao(CenarioAluguel c, Aluguel aluguel, DateTimeOffset data, decimal valorTotal)
    {
        c.Contexto.Set<Devolucao>().Add(new Devolucao(aluguel.Id, data, 1000m, 40m, NivelCombustivel.Cheio)
        {
            EmpresaId = c.EmpresaId,
            ValorTotal = valorTotal
        });
    }

    private static async Task<ResumoDashboardResult> CalcularAsync(CenarioAluguel c)
    {
        await c.Contexto.SaveChangesAsync();
        var resultado = await CriarHandler(c).Handle(new ObterResumoDashboardQuery(), CancellationToken.None);
        Assert.IsTrue(resultado.IsSuccess, resultado.IsFailed ? string.Join("; ", resultado.Errors) : "");
        return resultado.Value;
    }

    [TestMethod]
    public async Task SemMovimento_TudoZeradoEFrotaToda_Disponivel()
    {
        await using var c = await CenarioAluguel.CriarAsync(); // 1 automóvel, 1 cliente

        var r = await CalcularAsync(c);

        Assert.AreEqual(1, r.Frota.Total);
        Assert.AreEqual(0, r.Frota.EmUso);
        Assert.AreEqual(1, r.Frota.Disponiveis);
        Assert.AreEqual(0, r.Alugueis.EmAndamento);
        Assert.AreEqual(0m, r.Financeiro.ReceitaDoMes);
        Assert.AreEqual(1, r.TotalClientes);
        Assert.AreEqual(6, r.UltimosMeses.Count);
        Assert.AreEqual(Agora, r.GeradoEm);
    }

    [TestMethod]
    public async Task Frota_EmUsoEDisponiveis_ConsideramAndamentoEReservaQueCobreAgora()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var auto2 = await c.NovoAutomovelAsync("XYZ9Z99");
        var auto3 = await c.NovoAutomovelAsync("KKK0K00");
        NovoAluguel(c, c.Automovel, Dia(10, 10), Dia(10, 12), StatusAluguel.EmAndamento);  // em uso (e atrasado)
        NovoAluguel(c, auto2, Dia(10, 14), Dia(10, 20), StatusAluguel.Reservado);           // reserva que cobre agora
        NovoAluguel(c, auto3, Dia(11, 1), Dia(11, 5), StatusAluguel.Reservado);             // reserva futura: não bloqueia agora

        var r = await CalcularAsync(c);

        Assert.AreEqual(3, r.Frota.Total);
        Assert.AreEqual(1, r.Frota.EmUso);
        Assert.AreEqual(1, r.Frota.Disponiveis); // só o auto3
    }

    [TestMethod]
    public async Task Alugueis_ContamAndamentoReservadosEAtrasados()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var auto2 = await c.NovoAutomovelAsync("XYZ9Z99");
        NovoAluguel(c, c.Automovel, Dia(10, 10), Dia(10, 12), StatusAluguel.EmAndamento);   // atrasado (retorno 12/10 < agora)
        NovoAluguel(c, auto2, Dia(10, 14), Dia(10, 20), StatusAluguel.EmAndamento);         // em andamento no prazo
        NovoAluguel(c, c.Automovel, Dia(11, 1), Dia(11, 5), StatusAluguel.Reservado);
        NovoAluguel(c, c.Automovel, Dia(10, 3), Dia(10, 4), StatusAluguel.Cancelado);       // cancelado não conta

        var r = await CalcularAsync(c);

        Assert.AreEqual(2, r.Alugueis.EmAndamento);
        Assert.AreEqual(1, r.Alugueis.Reservados);
        Assert.AreEqual(1, r.Alugueis.Atrasados);
    }

    [TestMethod]
    public async Task Financeiro_SomaDevolucoesDoMesEComparaComMesAnterior()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var setembro = NovoAluguel(c, c.Automovel, Dia(9, 1), Dia(9, 4), StatusAluguel.Concluido);
        var outubro1 = NovoAluguel(c, c.Automovel, Dia(10, 1), Dia(10, 4), StatusAluguel.Concluido);
        var outubro2 = NovoAluguel(c, c.Automovel, Dia(10, 8), Dia(10, 12), StatusAluguel.Concluido);
        await c.Contexto.SaveChangesAsync();
        NovaDevolucao(c, setembro, Dia(9, 5), 500m);
        NovaDevolucao(c, outubro1, Dia(10, 5), 700m);
        NovaDevolucao(c, outubro2, Dia(10, 12), 300m);

        var r = await CalcularAsync(c);

        Assert.AreEqual(1000m, r.Financeiro.ReceitaDoMes);
        Assert.AreEqual(500m, r.Financeiro.ReceitaMesAnterior);
        Assert.AreEqual(2, r.Financeiro.DevolucoesDoMes);
    }

    [TestMethod]
    public async Task UltimosMeses_SaoSeisEmOrdemCronologicaComAlugueisEReceitaPorMes()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var a1 = NovoAluguel(c, c.Automovel, Dia(9, 1), Dia(9, 4), StatusAluguel.Concluido);
        NovoAluguel(c, c.Automovel, Dia(10, 1), Dia(10, 4), StatusAluguel.Concluido);
        NovoAluguel(c, c.Automovel, Dia(10, 10), Dia(10, 12), StatusAluguel.EmAndamento);
        NovoAluguel(c, c.Automovel, Dia(10, 3), Dia(10, 4), StatusAluguel.Cancelado);       // não entra
        NovoAluguel(c, c.Automovel, new DateTimeOffset(2026, 4, 30, 23, 0, 0, TimeSpan.Zero), Dia(5, 2), StatusAluguel.Concluido); // fora da janela (abril)
        await c.Contexto.SaveChangesAsync();
        NovaDevolucao(c, a1, Dia(9, 5), 500m);

        var r = await CalcularAsync(c);

        CollectionAssert.AreEqual(
            new[] { (2026, 5), (2026, 6), (2026, 7), (2026, 8), (2026, 9), (2026, 10) },
            r.UltimosMeses.Select(m => (m.Ano, m.Mes)).ToArray());
        var setembro = r.UltimosMeses.Single(m => m.Mes == 9);
        var outubro = r.UltimosMeses.Single(m => m.Mes == 10);
        Assert.AreEqual(1, setembro.Alugueis);
        Assert.AreEqual(500m, setembro.Receita);
        Assert.AreEqual(2, outubro.Alugueis);
        Assert.AreEqual(0m, outubro.Receita);
        Assert.AreEqual(0, r.UltimosMeses.Single(m => m.Mes == 5).Alugueis);
    }

    [TestMethod]
    public async Task Janela_AtravessaAVirada_DoAno()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var relogioEmFevereiro = new RelogioFixo(new DateTimeOffset(2027, 2, 10, 12, 0, 0, TimeSpan.Zero));
        var handler = new ObterResumoDashboardQueryHandler(c.Contexto, relogioEmFevereiro, NullLogger<ObterResumoDashboardQueryHandler>.Instance);

        var r = (await handler.Handle(new ObterResumoDashboardQuery(), CancellationToken.None)).Value;

        CollectionAssert.AreEqual(
            new[] { (2026, 9), (2026, 10), (2026, 11), (2026, 12), (2027, 1), (2027, 2) },
            r.UltimosMeses.Select(m => (m.Ano, m.Mes)).ToArray());
    }

    [TestMethod]
    public async Task NaoSomaDadosDeOutraEmpresa()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var outraEmpresa = Guid.NewGuid();
        c.Contexto.Set<Automovel>().Add(new Automovel("ZZZ9Z99", "Fiat", "Preto", "Uno", TipoCombustivel.Gasolina, 50, 2020, null, c.Grupo.Id)
        {
            EmpresaId = outraEmpresa
        });

        var r = await CalcularAsync(c);

        Assert.AreEqual(1, r.Frota.Total); // só o automóvel da empresa do tenant
    }
}
