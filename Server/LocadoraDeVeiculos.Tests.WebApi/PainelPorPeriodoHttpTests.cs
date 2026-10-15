using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using Microsoft.Extensions.DependencyInjection;

namespace LocadoraDeVeiculos.Tests.WebApi;

/// <summary>Relógio parado em um instante: o painel decide "este mês", "atrasado" e "indisponível agora" a partir dele.</summary>
internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}

/// <summary>
/// O painel com a data CONGELADA em 15/06/2030 e dados gravados nas bordas dos meses (último segundo de um mês, primeiro instante do
/// seguinte, fora da janela de 6 meses, cancelado, futuro). É o que separa "o painel soma algo" de "o painel soma os meses certos".
/// </summary>
[TestClass]
[TestCategory("Web API - Painel por período")]
public sealed class PainelPorPeriodoHttpTests
{
    private static readonly DateTimeOffset Agora = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static ApiFactory _fabrica = null!;

    [ClassInitialize]
    public static void Iniciar(TestContext _) =>
        _fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "100000" }, new RelogioFixo(Agora));

    [ClassCleanup]
    public static void Encerrar() => _fabrica.Dispose();

    private static DateTimeOffset D(int ano, int mes, int dia, int hora = 0, int minuto = 0, int segundo = 0) =>
        new(ano, mes, dia, hora, minuto, segundo, TimeSpan.Zero);

    private static async Task<EmpresaDeTeste> EmpresaComCenarioAsync()
    {
        var empresa = await EmpresaDeTeste.CriarAsync(_fabrica);
        using var escopo = _fabrica.Services.CreateScope();
        var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDeVeiculosDbContext>();
        var id = empresa.EmpresaId;

        var grupo = new GrupoAutomovel("Grupo do painel") { EmpresaId = id };
        var carros = Enumerable.Range(1, 4).Select(i => new Automovel($"PNL{i}A0{i}", "Fiat", "Prata", "Uno", TipoCombustivel.Gasolina, 50, 2022, null, grupo.Id) { EmpresaId = id }).ToList();
        var cliente = new ClientePessoaFisica("Cliente do painel", "(11) 99999-9999", "p@teste.com", "Rua Teste, 1", "111.444.777-35", "12.345.678-9", "11111111111", D(2035, 1, 1).UtcDateTime) { EmpresaId = id };
        var condutor = new Condutor("Condutor do painel", "c@teste.com", "529.982.247-25", "22222222222", D(2035, 1, 1).UtcDateTime, "(11) 98888-8888", cliente.Id) { EmpresaId = id };
        banco.AddRange(grupo, cliente, condutor);
        banco.AddRange(carros);

        Aluguel Novo(Automovel carro, DateTimeOffset saida, DateTimeOffset retorno, StatusAluguel status)
        {
            var a = new Aluguel(condutor.Id, carro.Id, cliente.Id, saida, retorno, 100m) { EmpresaId = id, Status = status };
            banco.Add(a);
            return a;
        }

        void Devolver(Aluguel aluguel, DateTimeOffset data, decimal valor) =>
            banco.Add(new Devolucao(aluguel.Id, data, 15_000m, 50m, NivelCombustivel.Cheio) { EmpresaId = id, ValorTotal = valor });

        // situação de HOJE (15/06)
        Novo(carros[0], D(2030, 6, 1), D(2030, 6, 10), StatusAluguel.EmAndamento);                   // A: em andamento e ATRASADO (devia ter voltado dia 10)
        Novo(carros[1], D(2030, 6, 12), D(2030, 6, 20), StatusAluguel.EmAndamento);                  // B: em andamento no prazo
        Novo(carros[2], D(2030, 6, 14), D(2030, 6, 16), StatusAluguel.Reservado);                    // C: reservado e o período cobre hoje: carro indisponível agora
        Novo(carros[3], D(2030, 7, 1), D(2030, 7, 5), StatusAluguel.Reservado);                      // D: reservado para o mês que vem (saída no 1º instante de julho)
        Novo(carros[3], D(2030, 6, 5), D(2030, 6, 7), StatusAluguel.Cancelado);                      // E: cancelado: não conta como saída

        // histórico: um aluguel concluído por devolução, para cada borda
        Devolver(Novo(carros[0], D(2030, 3, 31, 23, 59, 59), D(2030, 4, 2), StatusAluguel.Concluido), D(2030, 4, 1), 100m);          // saiu no último segundo de março; devolveu no 1º instante de abril
        Devolver(Novo(carros[1], D(2029, 12, 31, 23, 59, 59), D(2030, 1, 2), StatusAluguel.Concluido), D(2029, 12, 31, 23, 59, 59), 999m);   // dezembro: FORA da janela de 6 meses
        Devolver(Novo(carros[2], D(2030, 1, 1), D(2030, 1, 3), StatusAluguel.Concluido), D(2030, 1, 1), 10m);                         // 1º instante de janeiro: DENTRO da janela
        Devolver(Novo(carros[2], D(2030, 6, 1), D(2030, 6, 3), StatusAluguel.Concluido), D(2030, 6, 1), 200m);                        // 1º instante de junho: é de junho
        Devolver(Novo(carros[3], D(2030, 5, 31, 23, 59, 59), D(2030, 6, 30), StatusAluguel.Concluido), D(2030, 6, 30, 23, 59, 59), 50m);   // saiu em maio; devolveu no último segundo de junho
        Devolver(Novo(carros[3], D(2030, 4, 10), D(2030, 7, 2), StatusAluguel.Concluido), D(2030, 7, 1), 777m);                       // devolveu no 1º instante de julho: FORA (mês seguinte)

        await banco.SaveChangesAsync();
        return empresa;
    }

    [TestMethod]
    public async Task Painel_UsaOMesDoRelogioDaApi_ElistaOsSeisUltimosMesesEmOrdem()
    {
        using var empresa = await EmpresaComCenarioAsync();

        var resumo = await empresa.GetAsync("/api/dashboard/resumo");

        var meses = resumo.Propriedade("ultimosMeses").EnumerateArray().Select(m => (m.GetProperty("ano").GetInt32(), m.GetProperty("mes").GetInt32())).ToArray();
        CollectionAssert.AreEqual(new[] { (2030, 1), (2030, 2), (2030, 3), (2030, 4), (2030, 5), (2030, 6) }, meses);
        Assert.AreEqual(Agora, resumo.Propriedade("geradoEm").GetDateTimeOffset(), "o painel carimba a hora do relógio da API");
    }

    [TestMethod]
    public async Task Painel_SomaAReceitaPeloMesDaDevolucao_RespeitandoAsBordasDosMesesEAJanela()
    {
        using var empresa = await EmpresaComCenarioAsync();

        var resumo = await empresa.GetAsync("/api/dashboard/resumo");

        var receitaPorMes = resumo.Propriedade("ultimosMeses").EnumerateArray().Select(m => m.GetProperty("receita").GetDecimal()).ToArray();
        // jan 10 (1º instante) | fev 0 | mar 0 | abr 100 (1º instante de abril, não de março) | mai 0 | jun 200 + 50 (1º instante e último segundo de junho)
        CollectionAssert.AreEqual(new[] { 10m, 0m, 0m, 100m, 0m, 250m }, receitaPorMes, "999 (dez/29) e 777 (1º de julho) ficam fora da janela");
        var financeiro = resumo.Propriedade("financeiro");
        Assert.AreEqual(250m, financeiro.GetProperty("receitaDoMes").GetDecimal());
        Assert.AreEqual(0m, financeiro.GetProperty("receitaMesAnterior").GetDecimal(), "maio não teve devolução");
        Assert.AreEqual(2, financeiro.GetProperty("devolucoesDoMes").GetInt32());
    }

    [TestMethod]
    public async Task Painel_ContaAsSaidasPeloMesDaSaida_SemCanceladosEForaDaJanela()
    {
        using var empresa = await EmpresaComCenarioAsync();

        var resumo = await empresa.GetAsync("/api/dashboard/resumo");

        var saidasPorMes = resumo.Propriedade("ultimosMeses").EnumerateArray().Select(m => m.GetProperty("alugueis").GetInt32()).ToArray();
        // jan 1 (1º instante) | fev 0 | mar 1 (último segundo) | abr 1 | mai 1 (último segundo) | jun 4 (A, B, C e a concluída do dia 1; sem o cancelado nem o de 1º de julho)
        CollectionAssert.AreEqual(new[] { 1, 0, 1, 1, 1, 4 }, saidasPorMes, "o aluguel de dez/29 e o cancelado não entram");
    }

    [TestMethod]
    public async Task Painel_SeparaEmUsoDeIndisponivelAgora_EDeAtrasado()
    {
        using var empresa = await EmpresaComCenarioAsync();

        var resumo = await empresa.GetAsync("/api/dashboard/resumo");

        var frota = resumo.Propriedade("frota");
        Assert.AreEqual(4, frota.GetProperty("total").GetInt32());
        Assert.AreEqual(2, frota.GetProperty("emUso").GetInt32(), "dois carros com aluguel em andamento");
        Assert.AreEqual(1, frota.GetProperty("disponiveis").GetInt32(), "4 carros - 2 em uso - 1 reservado cujo período cobre hoje; a reserva de julho não bloqueia");
        var alugueis = resumo.Propriedade("alugueis");
        Assert.AreEqual(2, alugueis.GetProperty("emAndamento").GetInt32());
        Assert.AreEqual(2, alugueis.GetProperty("reservados").GetInt32(), "a de hoje e a de julho");
        Assert.AreEqual(1, alugueis.GetProperty("atrasados").GetInt32(), "só o que devia ter voltado dia 10; o que volta dia 20 está no prazo");
    }

    [TestMethod]
    public async Task Painel_DeOutraEmpresa_NaoVeNada_DosDadosDaPrimeira()
    {
        using var dona = await EmpresaComCenarioAsync();
        using var outra = await EmpresaDeTeste.CriarAsync(_fabrica);

        var resumo = await outra.GetAsync("/api/dashboard/resumo");

        Assert.AreEqual(0, resumo.Propriedade("frota").GetProperty("total").GetInt32());
        Assert.AreEqual(0, resumo.Propriedade("alugueis").GetProperty("atrasados").GetInt32());
        Assert.AreEqual(0m, resumo.Propriedade("financeiro").GetProperty("receitaDoMes").GetDecimal());
        Assert.IsTrue(resumo.Propriedade("ultimosMeses").EnumerateArray().All(m => m.GetProperty("receita").GetDecimal() == 0m && m.GetProperty("alugueis").GetInt32() == 0));
    }
}
