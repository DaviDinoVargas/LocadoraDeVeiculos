using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Tests.Integracao.Compartilhado;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutomovel;

/// <summary>
/// O formulário de aluguel listava TODOS os veículos (havia um TODO) e o conflito só aparecia como
/// 409 depois do envio. A listagem de disponíveis usa a mesma regra de sobreposição do cadastro.
/// </summary>
[TestClass]
[TestCategory("Integração - Automóveis disponíveis")]
public sealed class AutomoveisDisponiveisTests
{
    private static readonly DateTimeOffset Inicio = DateTimeOffset.UtcNow.AddDays(10);

    private static SelecionarAutomoveisDisponiveisQueryHandler CriarHandler(CenarioAluguel c) => new(
        c.RepositorioAutomovel, c.RepositorioAluguel, new SelecionarAutomoveisDisponiveisQueryValidator());

    private static async Task<Guid> AlugarAsync(CenarioAluguel c, Guid automovelId, DateTimeOffset saida, DateTimeOffset retorno)
    {
        var resultado = await c.CriarHandlerCadastro().Handle(
            new CadastrarAluguelCommand(c.Condutor.Id, automovelId, c.Cliente.Id, saida, retorno, 0m, new()),
            CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, resultado.IsFailed ? string.Join("; ", resultado.Errors) : "");
        return resultado.Value.Id;
    }

    private static async Task<string[]> PlacasDisponiveisAsync(
        CenarioAluguel c, DateTimeOffset saida, DateTimeOffset retorno, Guid? aluguelIdExcluir = null)
    {
        var resultado = await CriarHandler(c).Handle(
            new SelecionarAutomoveisDisponiveisQuery(saida, retorno, aluguelIdExcluir), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        return resultado.Value.Registros.Select(r => r.Placa).OrderBy(p => p).ToArray();
    }

    [TestMethod]
    public async Task SemAlugueis_TodosOsVeiculosEstaoDisponiveis()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await c.NovoAutomovelAsync("XYZ9Z99");

        var placas = await PlacasDisponiveisAsync(c, Inicio, Inicio.AddDays(2));

        CollectionAssert.AreEqual(new[] { "ABC1D23", "XYZ9Z99" }, placas);
    }

    [TestMethod]
    public async Task VeiculoComAluguelNoPeriodo_NaoApareceComoDisponivel()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var outro = await c.NovoAutomovelAsync("XYZ9Z99");
        await AlugarAsync(c, c.Automovel.Id, Inicio, Inicio.AddDays(3));

        var placas = await PlacasDisponiveisAsync(c, Inicio.AddDays(1), Inicio.AddDays(2)); // dentro do aluguel

        CollectionAssert.AreEqual(new[] { outro.Placa }, placas);
    }

    [TestMethod]
    public async Task PeriodoQueApenasCruzaOAluguel_TambemBloqueia()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await AlugarAsync(c, c.Automovel.Id, Inicio, Inicio.AddDays(3));

        Assert.AreEqual(0, (await PlacasDisponiveisAsync(c, Inicio.AddDays(-2), Inicio.AddDays(1))).Length);  // cruza o começo
        Assert.AreEqual(0, (await PlacasDisponiveisAsync(c, Inicio.AddDays(2), Inicio.AddDays(6))).Length);   // cruza o fim
        Assert.AreEqual(0, (await PlacasDisponiveisAsync(c, Inicio.AddDays(-1), Inicio.AddDays(5))).Length);  // engloba
    }

    [TestMethod]
    public async Task PeriodoDepoisDoAluguel_VeiculoVoltaADisponivel()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await AlugarAsync(c, c.Automovel.Id, Inicio, Inicio.AddDays(3));

        var placas = await PlacasDisponiveisAsync(c, Inicio.AddDays(4), Inicio.AddDays(6));

        CollectionAssert.AreEqual(new[] { "ABC1D23" }, placas);
    }

    [TestMethod]
    public async Task AluguelCancelado_NaoBloqueiaOVeiculo()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var id = await AlugarAsync(c, c.Automovel.Id, Inicio, Inicio.AddDays(3));
        var aluguel = await c.RepositorioAluguel.SelecionarAluguelCompletoPorIdAsync(id);
        aluguel!.Status = StatusAluguel.Cancelado;
        await c.Contexto.SaveChangesAsync();

        var placas = await PlacasDisponiveisAsync(c, Inicio, Inicio.AddDays(3));

        CollectionAssert.AreEqual(new[] { "ABC1D23" }, placas);
    }

    [TestMethod]
    public async Task NaEdicao_OProprioAluguelNaoBloqueiaOVeiculoQueJaUsa()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var id = await AlugarAsync(c, c.Automovel.Id, Inicio, Inicio.AddDays(3));

        var semExcluir = await PlacasDisponiveisAsync(c, Inicio, Inicio.AddDays(3));
        var comExcluir = await PlacasDisponiveisAsync(c, Inicio, Inicio.AddDays(3), aluguelIdExcluir: id);

        Assert.AreEqual(0, semExcluir.Length);
        CollectionAssert.AreEqual(new[] { "ABC1D23" }, comExcluir);
    }

    [TestMethod]
    public async Task ListaDeDisponiveisEOCadastroConcordam_VeiculoIndisponivelTomaConflitoAoSalvar()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await AlugarAsync(c, c.Automovel.Id, Inicio, Inicio.AddDays(3));
        var saida = Inicio.AddDays(1);
        var retorno = Inicio.AddDays(2);

        Assert.AreEqual(0, (await PlacasDisponiveisAsync(c, saida, retorno)).Length);

        var tentativa = await c.CriarHandlerCadastro().Handle(
            new CadastrarAluguelCommand(c.Condutor.Id, c.Automovel.Id, c.Cliente.Id, saida, retorno, 0m, new()),
            CancellationToken.None);
        Assert.IsTrue(tentativa.IsFailed);
    }

    [TestMethod]
    public async Task RetornoAntesDaSaida_EhRejeitado()
    {
        await using var c = await CenarioAluguel.CriarAsync();

        var resultado = await CriarHandler(c).Handle(
            new SelecionarAutomoveisDisponiveisQuery(Inicio, Inicio.AddDays(-1), null), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    // ---- veículo com aluguel EM ANDAMENTO atrasado (o carro ainda não voltou) ----

    private static async Task<Aluguel> AluguelEmAndamentoAsync(CenarioAluguel c, Guid automovelId, int diasDeSaida, int diasDeRetornoPrevisto)
    {
        var agora = DateTimeOffset.UtcNow;
        var aluguel = new Aluguel(c.Condutor.Id, automovelId, c.Cliente.Id, agora.AddDays(diasDeSaida), agora.AddDays(diasDeRetornoPrevisto), 300m)
        {
            EmpresaId = c.EmpresaId,
            Status = StatusAluguel.EmAndamento
        };
        c.Contexto.Set<Aluguel>().Add(aluguel);
        await c.Contexto.SaveChangesAsync();
        return aluguel;
    }

    [TestMethod]
    public async Task AluguelEmAndamentoAtrasado_BloqueiaOVeiculoNoPeriodoSeguinte()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var outro = await c.NovoAutomovelAsync("XYZ9Z99");
        await AluguelEmAndamentoAsync(c, c.Automovel.Id, diasDeSaida: -10, diasDeRetornoPrevisto: -5);   // devia ter voltado há 5 dias

        var agora = DateTimeOffset.UtcNow;
        var placas = await PlacasDisponiveisAsync(c, agora, agora.AddDays(2));

        CollectionAssert.AreEqual(new[] { outro.Placa }, placas);   // o carro que não voltou NÃO está disponível
    }

    [TestMethod]
    public async Task AluguelEmAndamentoNoPrazo_ComPeriodoDepoisDoRetorno_ContinuaLiberandoOVeiculo()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await AluguelEmAndamentoAsync(c, c.Automovel.Id, diasDeSaida: -1, diasDeRetornoPrevisto: 2);     // dentro do prazo

        var depois = DateTimeOffset.UtcNow.AddDays(5);
        var placas = await PlacasDisponiveisAsync(c, depois, depois.AddDays(2));

        CollectionAssert.AreEqual(new[] { "ABC1D23" }, placas);   // reserva futura é possível: ele volta antes
    }

    [TestMethod]
    public async Task AposARegistrarDevolucao_OVeiculoAtrasadoVoltaADisponivel()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var aluguel = await AluguelEmAndamentoAsync(c, c.Automovel.Id, diasDeSaida: -10, diasDeRetornoPrevisto: -5);
        var agora = DateTimeOffset.UtcNow;

        aluguel.Status = StatusAluguel.Concluido;
        await c.Contexto.SaveChangesAsync();

        CollectionAssert.AreEqual(new[] { "ABC1D23" }, await PlacasDisponiveisAsync(c, agora, agora.AddDays(2)));
    }

    [TestMethod]
    public async Task AluguelReservadoComPeriodoVencido_NaoBloqueiaOVeiculo()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        var aluguel = await AluguelEmAndamentoAsync(c, c.Automovel.Id, diasDeSaida: -10, diasDeRetornoPrevisto: -5);
        aluguel.Status = StatusAluguel.Reservado;   // nunca foi retirado: não há carro fora
        await c.Contexto.SaveChangesAsync();
        var agora = DateTimeOffset.UtcNow;

        CollectionAssert.AreEqual(new[] { "ABC1D23" }, await PlacasDisponiveisAsync(c, agora, agora.AddDays(2)));
    }

    [TestMethod]
    public async Task VeiculoAtrasado_NaoPodeSerAlugadoDeNovo_CadastroTomaConflito()
    {
        await using var c = await CenarioAluguel.CriarAsync();
        await AluguelEmAndamentoAsync(c, c.Automovel.Id, diasDeSaida: -10, diasDeRetornoPrevisto: -5);
        var agora = DateTimeOffset.UtcNow;

        var tentativa = await c.CriarHandlerCadastro().Handle(
            new CadastrarAluguelCommand(c.Condutor.Id, c.Automovel.Id, c.Cliente.Id, agora.AddHours(1), agora.AddDays(2), 0m, new()),
            CancellationToken.None);

        Assert.IsTrue(tentativa.IsFailed);   // a lista e o cadastro concordam
    }
}
