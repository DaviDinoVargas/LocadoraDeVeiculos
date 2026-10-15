using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace LocadoraDeVeiculos.Tests.WebApi;

/// <summary>
/// O coração do negócio pela API de verdade: preço calculado no servidor (diárias, taxas fixas e diárias, cupom, caução), reserva sem
/// sobreposição, ciclo de vida do aluguel (reservar, iniciar, cancelar, excluir) e a devolução com multa por atraso e combustível.
/// Os valores esperados são contas feitas à mão, não cópias do código.
/// </summary>
[TestClass]
[TestCategory("Web API - Aluguel e devolução")]
public sealed class AluguelEDevolucaoHttpTests
{
    private static ApiFactory _fabrica = null!;

    [ClassInitialize]
    public static void Iniciar(TestContext _) => _fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "100000" });

    [ClassCleanup]
    public static void Encerrar() => _fabrica.Dispose();

    private static Task<EmpresaDeTeste> NovaEmpresa(string cargo = "Empresa") => EmpresaDeTeste.CriarAsync(_fabrica, cargo);

    // ---------------------------------------------------------------- preço -----------------------------------------------------

    [TestMethod]
    public async Task ValorPrevisto_EDiariasMaisTaxas_ECalculadoNoServidor_ValorEnviadoPeloClienteEIgnorado()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();                       // plano: R$ 100 por diária
        var fixa = (await empresa.CriarTaxaAsync("GPS", 50m, "Fixo")).Id;       // R$ 50 uma vez
        var porDia = (await empresa.CriarTaxaAsync("Cadeirinha", 20m, "Diario")).Id; // R$ 20 por dia

        var criado = await empresa.CriarAluguelAsync(cenario, duracao: 3, taxas: [fixa, porDia]);   // o corpo manda valorPrevisto = 1

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        var aluguel = await empresa.GetAsync($"/api/alugueis/{criado.Id}");
        // 3 diárias x 100 + taxa fixa 50 + taxa diária 3 x 20 = 410
        Assert.AreEqual(410m, aluguel.Propriedade("valorPrevisto").GetDecimal());
        Assert.AreEqual("Reservado", aluguel.Propriedade("status").GetString());
        Assert.AreEqual(2, aluguel.Propriedade("taxasServicos").GetArrayLength());
    }

    [TestMethod]
    public async Task Simulacao_DevolveAMesmaContaQueDepoisSeGrava_ComACaucaoVigente()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        await empresa.DefinirValorDaGarantiaAsync(750m);
        var taxa = (await empresa.CriarTaxaAsync("Cadeirinha", 20m, "Diario")).Id;
        var saida = DateTimeOffset.UtcNow.AddDays(5);

        var simulacao = await empresa.PostAsync("/api/alugueis/simular", new
        {
            automovelId = cenario.Automoveis[0], dataSaida = saida, dataRetornoPrevisto = saida.AddDays(4), taxasServicosIds = new[] { taxa }
        });
        var criado = await empresa.CriarAluguelAsync(cenario, diasAteSaida: 5, duracao: 4, taxas: [taxa]);

        Assert.IsTrue(simulacao.Ok, simulacao.TextoDosErros);
        Assert.AreEqual(4, simulacao.Propriedade("dias").GetInt32());
        Assert.AreEqual(400m, simulacao.Propriedade("valorDiarias").GetDecimal());
        Assert.AreEqual(80m, simulacao.Propriedade("valorTaxas").GetDecimal());
        Assert.AreEqual(480m, simulacao.Propriedade("valorPrevisto").GetDecimal());
        Assert.AreEqual(750m, simulacao.Propriedade("valorCaucao").GetDecimal());
        var gravado = await empresa.GetAsync($"/api/alugueis/{criado.Id}");
        Assert.AreEqual(simulacao.Propriedade("valorPrevisto").GetDecimal(), gravado.Propriedade("valorPrevisto").GetDecimal(), "o que a tela mostra é o que será cobrado");
        Assert.AreEqual(750m, gravado.Propriedade("valorCaucao").GetDecimal());
    }

    [TestMethod]
    public async Task Caucao_FicaRegistradaNoAluguel_EMudarAConfiguracaoDepoisNaoAlteraOsJaFeitos()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        await empresa.DefinirValorDaGarantiaAsync(500m);
        var antes = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0]);

        await empresa.DefinirValorDaGarantiaAsync(2000m);
        var depois = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1]);

        Assert.AreEqual(500m, (await empresa.GetAsync($"/api/alugueis/{antes.Id}")).Propriedade("valorCaucao").GetDecimal());
        Assert.AreEqual(2000m, (await empresa.GetAsync($"/api/alugueis/{depois.Id}")).Propriedade("valorCaucao").GetDecimal());
    }

    [TestMethod]
    public async Task Cupom_Percentual_DescontaDoTotal_RegistraOUso_ELimiteDeUsosEsgota()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        await empresa.CriarCupomAsync("PROMO10", "Percentual", 10m, limiteUsos: 1);

        var primeiro = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0], duracao: 3, cupom: "promo10");   // diárias 300, 10% = 30
        var segundo = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1], duracao: 3, cupom: "PROMO10");

        Assert.AreEqual(HttpStatusCode.Created, primeiro.Status, primeiro.TextoDosErros);
        var aluguel = await empresa.GetAsync($"/api/alugueis/{primeiro.Id}");
        Assert.AreEqual(270m, aluguel.Propriedade("valorPrevisto").GetDecimal());
        Assert.AreEqual(30m, aluguel.Propriedade("valorDesconto").GetDecimal());
        Assert.AreEqual("PROMO10", aluguel.Propriedade("cupomCodigo").GetString(), "o código é guardado em maiúsculas");
        Assert.AreEqual(HttpStatusCode.BadRequest, segundo.Status);
        StringAssert.Contains(segundo.TextoDosErros, "Cupom inválido, expirado ou esgotado");
        Assert.AreEqual(1, (await empresa.GetAsync("/api/alugueis")).Registros.Count, "o aluguel recusado não foi gravado");
    }

    [TestMethod]
    public async Task Cupom_DeValorFixoMaiorQueOTotal_NaoDeixaOAluguelNegativo()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        await empresa.CriarCupomAsync("GIGANTE", "ValorFixo", 5000m);

        var criado = await empresa.CriarAluguelAsync(cenario, duracao: 2, cupom: "GIGANTE");   // total 200, desconto limitado a 200

        var aluguel = await empresa.GetAsync($"/api/alugueis/{criado.Id}");
        Assert.AreEqual(0m, aluguel.Propriedade("valorPrevisto").GetDecimal());
        Assert.AreEqual(200m, aluguel.Propriedade("valorDesconto").GetDecimal());
    }

    [TestMethod]
    public async Task Cupom_Inexistente_Expirado_OuInativo_EhRecusado()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 3);
        var expirado = (await empresa.CriarCupomAsync("VELHO", validoAte: DateTimeOffset.UtcNow.AddMilliseconds(300))).Id;
        var inativo = (await empresa.CriarCupomAsync("DESLIGADO")).Id;
        await empresa.PutAsync($"/api/cupons/{inativo}", new
        {
            codigo = "DESLIGADO", descricao = "Cupom de teste", tipoDesconto = "Percentual", valorDesconto = 10m,
            validoAte = DateTimeOffset.UtcNow.AddDays(60), limiteUsos = (int?)null, parceiroId = (Guid?)null, ativo = false
        });
        await Task.Delay(500);

        foreach (var (codigo, carro) in new[] { ("NAOEXISTE", 0), ("VELHO", 1), ("DESLIGADO", 2) })
        {
            var resposta = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[carro], cupom: codigo);
            Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status, codigo);
            StringAssert.Contains(resposta.TextoDosErros, "Cupom inválido, expirado ou esgotado", codigo);
        }

        Assert.AreEqual(0, (await empresa.GetAsync("/api/alugueis")).Registros.Count);
        Assert.IsNotNull(expirado);
    }

    [TestMethod]
    public async Task Taxa_Inexistente_NaoEIgnoradaEmSilencio_OAluguelEhRecusado()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();

        var resposta = await empresa.CriarAluguelAsync(cenario, taxas: [Guid.NewGuid()]);

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status);
        StringAssert.Contains(resposta.TextoDosErros, "taxas de serviço informadas não existem");
        Assert.AreEqual(0, (await empresa.GetAsync("/api/alugueis")).Registros.Count);
    }

    [TestMethod]
    public async Task Taxa_DeOutraEmpresa_NaoPodeSerUsadaNoAluguel()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var taxaDaDona = (await dona.CriarTaxaAsync("Só da dona", 99m)).Id;
        var cenario = await intrusa.MontarCenarioAsync();

        var resposta = await intrusa.CriarAluguelAsync(cenario, taxas: [taxaDaDona]);

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/alugueis")).Registros.Count);
    }

    // ---------------------------------------------------------------- reserva ---------------------------------------------------

    [TestMethod]
    public async Task Reserva_DoMesmoCarroNoMesmoPeriodo_Recebe409_MasOutroPeriodoOuOutroCarroPodem()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        var primeira = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0], diasAteSaida: 5, duracao: 3);

        var sobreposta = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0], diasAteSaida: 6, duracao: 3);
        var outroCarro = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1], diasAteSaida: 6, duracao: 3);
        var depois = await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0], diasAteSaida: 20, duracao: 3);

        Assert.AreEqual(HttpStatusCode.Created, primeira.Status, primeira.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.Conflict, sobreposta.Status);
        StringAssert.Contains(sobreposta.TextoDosErros, "já está reservado para este período");
        Assert.AreEqual(HttpStatusCode.Created, outroCarro.Status, "outro carro no mesmo período está livre");
        Assert.AreEqual(HttpStatusCode.Created, depois.Status, "o mesmo carro em outro período está livre");
    }

    [TestMethod]
    public async Task Reserva_CancelarLiberaOCarroParaOMesmoPeriodo()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        var primeira = (await empresa.CriarAluguelAsync(cenario)).Id;
        Assert.AreEqual(HttpStatusCode.Conflict, (await empresa.CriarAluguelAsync(cenario)).Status);

        Assert.IsTrue((await empresa.CancelarAluguelAsync(primeira)).Ok);
        var denovo = await empresa.CriarAluguelAsync(cenario);

        Assert.AreEqual(HttpStatusCode.Created, denovo.Status, "o aluguel cancelado não segura mais o carro: " + denovo.TextoDosErros);
    }

    [TestMethod]
    public async Task Reserva_ComCnhDoCondutorVencida_EhRecusada()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        using (var escopo = _fabrica.Services.CreateScope())
        {
            var banco = escopo.ServiceProvider.GetRequiredService<LocadoraDeVeiculosDbContext>();
            var condutor = await banco.Condutores.IgnoreQueryFilters().SingleAsync(c => c.Id == cenario.Condutor);
            condutor.ValidadeCnh = DateTime.UtcNow.AddDays(-1);      // a CNH venceu depois do cadastro
            await banco.SaveChangesAsync();
        }

        var resposta = await empresa.CriarAluguelAsync(cenario);

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status);
        StringAssert.Contains(resposta.TextoDosErros, "CNH vencida");
        Assert.AreEqual(0, (await empresa.GetAsync("/api/alugueis")).Registros.Count);
    }

    [TestMethod]
    public async Task Reserva_ComDatasInvalidas_Recebe400ComAMensagemDeCadaRegra()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();

        var noPassado = await empresa.CriarAluguelAsync(cenario, diasAteSaida: -3, duracao: 2);
        var retornoAntes = await empresa.CriarAluguelAsync(cenario, diasAteSaida: 5, duracao: -1);
        var longo = await empresa.CriarAluguelAsync(cenario, diasAteSaida: 5, duracao: 400);

        StringAssert.Contains(noPassado.TextoDosErros, "data de saída deve ser futura");
        StringAssert.Contains(retornoAntes.TextoDosErros, "deve ser após a data de saída");
        StringAssert.Contains(longo.TextoDosErros, "não pode passar de 365 dias");
        Assert.AreEqual(0, (await empresa.GetAsync("/api/alugueis")).Registros.Count);
    }

    [TestMethod]
    public async Task Reserva_ComCarroClienteOuCondutorDeOutraEmpresa_EhRecusada()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var deles = await dona.MontarCenarioAsync();
        var meu = await intrusa.MontarCenarioAsync();

        var comCarroAlheio = await intrusa.CriarAluguelAsync(meu with { Automoveis = deles.Automoveis });
        var comClienteAlheio = await intrusa.CriarAluguelAsync(meu with { Cliente = deles.Cliente });
        var comCondutorAlheio = await intrusa.CriarAluguelAsync(meu with { Condutor = deles.Condutor });

        Assert.IsFalse(comCarroAlheio.Ok, "carro de outra empresa");
        Assert.IsFalse(comClienteAlheio.Ok, "cliente de outra empresa");
        Assert.IsFalse(comCondutorAlheio.Ok, "condutor de outra empresa");
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/alugueis")).Registros.Count);
        Assert.AreEqual(0, (await dona.GetAsync("/api/alugueis")).Registros.Count, "nada vazou para a dona");
    }

    [TestMethod]
    public async Task Reserva_DeCondutorQueNaoPertenceAoCliente_EhRecusada()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        var outroCliente = (await empresa.CriarClienteAsync("Outro Cliente")).Id;
        var condutorDeOutro = (await empresa.CriarCondutorAsync(outroCliente)).Id;

        var resposta = await empresa.CriarAluguelAsync(cenario with { Condutor = condutorDeOutro });

        Assert.IsFalse(resposta.Ok, "a tela diz que o condutor precisa pertencer ao cliente; a API precisa impor isso também");
    }

    // ---------------------------------------------------------------- ciclo de vida ----------------------------------------------

    [TestMethod]
    public async Task Aluguel_Iniciar_PassaDeReservadoParaEmAndamento_ENaoPodeIniciarDeNovo()
    {
        using var empresa = await NovaEmpresa();
        var aluguel = (await empresa.CriarAluguelAsync(await empresa.MontarCenarioAsync())).Id;

        var iniciado = await empresa.IniciarAluguelAsync(aluguel);
        var denovo = await empresa.IniciarAluguelAsync(aluguel);

        Assert.IsTrue(iniciado.Ok, iniciado.TextoDosErros);
        Assert.AreEqual("EmAndamento", iniciado.Propriedade("status").GetString());
        Assert.AreEqual("EmAndamento", (await empresa.GetAsync($"/api/alugueis/{aluguel}")).Propriedade("status").GetString());
        Assert.AreEqual(HttpStatusCode.Conflict, denovo.Status);
    }

    [TestMethod]
    public async Task Aluguel_Cancelar_SoReservado_EmAndamentoEConcluidoRecebem409()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 3);
        var reservado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0])).Id;
        var emAndamento = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.IniciarAluguelAsync(emAndamento);
        var concluido = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[2])).Id;
        await empresa.IniciarAluguelAsync(concluido);
        await empresa.RegistrarDevolucaoAsync(concluido, DateTimeOffset.UtcNow.AddDays(9));

        var cancelado = await empresa.CancelarAluguelAsync(reservado);
        var andamento = await empresa.CancelarAluguelAsync(emAndamento);
        var jaConcluido = await empresa.CancelarAluguelAsync(concluido);

        Assert.IsTrue(cancelado.Ok, cancelado.TextoDosErros);
        Assert.AreEqual("Cancelado", (await empresa.GetAsync($"/api/alugueis/{reservado}")).Propriedade("status").GetString());
        Assert.AreEqual(HttpStatusCode.Conflict, andamento.Status);
        StringAssert.Contains(andamento.TextoDosErros, "aluguel em andamento");
        Assert.AreEqual(HttpStatusCode.Conflict, jaConcluido.Status);
        StringAssert.Contains(jaConcluido.TextoDosErros, "já concluído");
    }

    [TestMethod]
    public async Task Aluguel_Excluir_SoReservadoOuCancelado_EmAndamentoEConcluidoRecebem409()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 3);
        var reservado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0])).Id;
        var emAndamento = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.IniciarAluguelAsync(emAndamento);
        var cancelado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[2])).Id;
        await empresa.CancelarAluguelAsync(cancelado);

        var excluirReservado = await empresa.DeleteAsync($"/api/alugueis/{reservado}");
        var excluirAndamento = await empresa.DeleteAsync($"/api/alugueis/{emAndamento}");
        var excluirCancelado = await empresa.DeleteAsync($"/api/alugueis/{cancelado}");

        Assert.IsTrue(excluirReservado.Ok, excluirReservado.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/alugueis/{reservado}")).Status);
        Assert.AreEqual(HttpStatusCode.Conflict, excluirAndamento.Status);
        StringAssert.Contains(excluirAndamento.TextoDosErros, "em andamento ou concluído");
        Assert.AreEqual(HttpStatusCode.OK, (await empresa.GetAsync($"/api/alugueis/{emAndamento}")).Status);
        Assert.IsTrue(excluirCancelado.Ok);
    }

    [TestMethod]
    public async Task Aluguel_Editar_RecalculaOValor_ESoPodeEditarReservadoOuEmAndamento()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        var aluguel = (await empresa.CriarAluguelAsync(cenario, duracao: 3)).Id;     // 300
        var cancelado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.CancelarAluguelAsync(cancelado);
        var saida = DateTimeOffset.UtcNow.AddDays(5);

        object Corpo(Guid id, int dias) => new
        {
            condutorId = cenario.Condutor, automovelId = id, clienteId = cenario.Cliente, dataSaida = saida, dataRetornoPrevisto = saida.AddDays(dias),
            valorPrevisto = 1m, taxasServicosIds = Array.Empty<Guid>()
        };

        var editado = await empresa.PutAsync($"/api/alugueis/{aluguel}", Corpo(cenario.Automoveis[0], 5));
        var editarCancelado = await empresa.PutAsync($"/api/alugueis/{cancelado}", Corpo(cenario.Automoveis[1], 2));

        Assert.IsTrue(editado.Ok, editado.TextoDosErros);
        Assert.AreEqual(500m, (await empresa.GetAsync($"/api/alugueis/{aluguel}")).Propriedade("valorPrevisto").GetDecimal(), "5 diárias de R$ 100");
        Assert.AreEqual(HttpStatusCode.Conflict, editarCancelado.Status);
        StringAssert.Contains(editarCancelado.TextoDosErros, "concluído ou cancelado");
    }

    [TestMethod]
    public async Task Aluguel_Listas_PorStatusEEmAberto_SeparamOsAlugueisCertos()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 3);
        var reservado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0])).Id;
        var andamento = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.IniciarAluguelAsync(andamento);
        var cancelado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[2])).Id;
        await empresa.CancelarAluguelAsync(cancelado);

        static Guid[] Ids(Resposta r) => r.Registros.Select(x => x.GetProperty("id").GetGuid()).OrderBy(x => x).ToArray();

        CollectionAssert.AreEqual(new[] { reservado }, Ids(await empresa.GetAsync("/api/alugueis/status/Reservado")));
        CollectionAssert.AreEqual(new[] { andamento }, Ids(await empresa.GetAsync("/api/alugueis/status/EmAndamento")));
        CollectionAssert.AreEqual(new[] { cancelado }, Ids(await empresa.GetAsync("/api/alugueis/status/Cancelado")));
        CollectionAssert.AreEqual(new[] { reservado, andamento }.OrderBy(x => x).ToArray(), Ids(await empresa.GetAsync("/api/alugueis/em-aberto")), "em aberto = reservado + em andamento");
        Assert.AreEqual(HttpStatusCode.BadRequest, (await empresa.GetAsync("/api/alugueis/status/Inventado")).Status);
        Assert.AreEqual(3, (await empresa.GetAsync("/api/alugueis")).Registros.Count);
    }

    [TestMethod]
    public async Task Aluguel_DeOutraEmpresa_EInvisivel_NaoPodeSerLidoIniciadoCanceladoNemExcluido()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var aluguel = (await dona.CriarAluguelAsync(await dona.MontarCenarioAsync())).Id;

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/alugueis/{aluguel}")).Status);
        Assert.IsFalse((await intrusa.IniciarAluguelAsync(aluguel)).Ok);
        Assert.IsFalse((await intrusa.CancelarAluguelAsync(aluguel)).Ok);
        Assert.IsFalse((await intrusa.DeleteAsync($"/api/alugueis/{aluguel}")).Ok);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/alugueis")).Registros.Count);
        Assert.AreEqual("Reservado", (await dona.GetAsync($"/api/alugueis/{aluguel}")).Propriedade("status").GetString(), "as tentativas não mudaram o aluguel da dona");
    }

    [TestMethod]
    public async Task Funcionario_ReservaEIniciaAlugueis_DaEmpresaDele()
    {
        using var dona = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, dona, "Funcionario");
        var cenario = await dona.MontarCenarioAsync();

        var criado = await funcionario.CriarAluguelAsync(cenario);
        var iniciado = await funcionario.IniciarAluguelAsync(criado.Id);

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        Assert.IsTrue(iniciado.Ok, iniciado.TextoDosErros);
        Assert.AreEqual(1, (await dona.GetAsync("/api/alugueis")).Registros.Count);
    }

    // ---------------------------------------------------------------- devolução ---------------------------------------------------

    private static async Task<(Guid Aluguel, Cenario Cenario)> AluguelEmAndamentoAsync(EmpresaDeTeste empresa, int duracao = 3, string? cupom = null)
    {
        var cenario = await empresa.MontarCenarioAsync();
        var aluguel = (await empresa.CriarAluguelAsync(cenario, diasAteSaida: 5, duracao: duracao, cupom: cupom)).Id;
        await empresa.IniciarAluguelAsync(aluguel);
        return (aluguel, cenario);
    }

    [TestMethod]
    public async Task Devolucao_NoPrazoComTanqueCheio_NaoTemMultaNemAdicional_ETotalEIgualAoPrevisto()
    {
        using var empresa = await NovaEmpresa();
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa, duracao: 3);          // previsto 300

        var devolucao = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7), litros: 50m, nivel: "Cheio");

        Assert.AreEqual(HttpStatusCode.Created, devolucao.Status, devolucao.TextoDosErros);
        Assert.AreEqual(0m, devolucao.Propriedade("valorMultas").GetDecimal());
        Assert.AreEqual(0m, devolucao.Propriedade("valorAdicionalCombustivel").GetDecimal());
        Assert.AreEqual(300m, devolucao.Propriedade("valorTotal").GetDecimal());
        Assert.AreEqual("Concluido", (await empresa.GetAsync($"/api/alugueis/{aluguel}")).Propriedade("status").GetString(), "devolver conclui o aluguel");
    }

    [TestMethod]
    public async Task Devolucao_ComAtraso_CobraMultaProporcionalAosDiasDeAtraso()
    {
        using var empresa = await NovaEmpresa();
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa, duracao: 3);          // previsto 300, diária efetiva 100
        var retornoPrevisto = (await empresa.GetAsync($"/api/alugueis/{aluguel}")).Propriedade("dataRetornoPrevisto").GetDateTimeOffset();

        // exatamente 2 dias depois do prazo (a API cobra por dia de atraso iniciado, então partir do prazo real evita uma fração de dia a mais)
        var devolucao = await empresa.RegistrarDevolucaoAsync(aluguel, retornoPrevisto.AddDays(2), litros: 50m);

        // multa = diária (100) x 10 % x 2 dias = 20
        Assert.AreEqual(20m, Math.Round(devolucao.Propriedade("valorMultas").GetDecimal(), 2));
        Assert.AreEqual(320m, Math.Round(devolucao.Propriedade("valorTotal").GetDecimal(), 2));
    }

    [TestMethod]
    public async Task Devolucao_ComTanqueAbaixoDoNivelExigido_CobraOsLitrosQueFaltam_PeloPrecoDaConfiguracao()
    {
        using var empresa = await NovaEmpresa();
        await empresa.DefinirPrecoDoCombustivelAsync(6m);
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa, duracao: 3);

        // tanque de 50 L, nível exigido Cheio (100 %), devolvido com 40 L (80 %): faltam 10 L x R$ 6 = 60
        var devolucao = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7), litros: 40m, nivel: "Cheio");

        Assert.AreEqual(60m, devolucao.Propriedade("valorAdicionalCombustivel").GetDecimal());
        Assert.AreEqual(360m, devolucao.Propriedade("valorTotal").GetDecimal());
    }

    [TestMethod]
    public async Task Devolucao_ComTanqueNoNivelExigido_NaoCobraCombustivel()
    {
        using var empresa = await NovaEmpresa();
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa);

        // nível exigido Metade (50 %); devolvido com 30 L de 50 (60 %): atende
        var devolucao = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7), litros: 30m, nivel: "Metade");

        Assert.AreEqual(0m, devolucao.Propriedade("valorAdicionalCombustivel").GetDecimal());
    }

    [TestMethod]
    public async Task Devolucao_ComTanqueVazio_PodeSerRegistrada_ECobraOTanqueTodo()
    {
        using var empresa = await NovaEmpresa();
        await empresa.DefinirPrecoDoCombustivelAsync(5m);
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa);

        var devolucao = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7), litros: 0m, nivel: "Cheio");

        Assert.AreEqual(HttpStatusCode.Created, devolucao.Status, "um carro devolvido sem combustível é um caso real e precisa ser registrável: " + devolucao.TextoDosErros);
        Assert.AreEqual(250m, devolucao.Propriedade("valorAdicionalCombustivel").GetDecimal(), "50 L x R$ 5");
    }

    [TestMethod]
    public async Task Devolucao_ValorTotalUsaOValorJaComDescontoDoCupom()
    {
        using var empresa = await NovaEmpresa();
        await empresa.CriarCupomAsync("DEZ", "Percentual", 10m);
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa, duracao: 3, cupom: "DEZ");     // 300 - 30 = 270

        var devolucao = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7));

        Assert.AreEqual(270m, devolucao.Propriedade("valorTotal").GetDecimal());
    }

    [TestMethod]
    public async Task Devolucao_SoDeAluguelEmAndamento_E_UmaPorAluguel()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        var reservado = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0])).Id;
        var andamento = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.IniciarAluguelAsync(andamento);

        var deReservado = await empresa.RegistrarDevolucaoAsync(reservado, DateTimeOffset.UtcNow.AddDays(7));
        var primeira = await empresa.RegistrarDevolucaoAsync(andamento, DateTimeOffset.UtcNow.AddDays(7));
        var segunda = await empresa.RegistrarDevolucaoAsync(andamento, DateTimeOffset.UtcNow.AddDays(7));

        Assert.AreEqual(HttpStatusCode.Conflict, deReservado.Status);
        StringAssert.Contains(deReservado.TextoDosErros, "aluguéis em andamento");
        Assert.AreEqual(HttpStatusCode.Created, primeira.Status);
        Assert.IsFalse(segunda.Ok, "o aluguel já está concluído: a segunda devolução não pode passar");
        Assert.AreEqual(1, (await empresa.GetAsync("/api/devolucoes")).Registros.Count);
    }

    [TestMethod]
    public async Task Devolucao_ComDadosInvalidos_Recebe400()
    {
        using var empresa = await NovaEmpresa();
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa);

        var semKm = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7), km: 0m);
        var combustivelNegativo = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7), litros: -5m);
        var nivelInventado = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7), nivel: "Transbordando");

        StringAssert.Contains(semKm.TextoDosErros, "quilometragem final deve ser maior que zero");
        StringAssert.Contains(combustivelNegativo.TextoDosErros, "não pode ser negativo");
        Assert.IsFalse(nivelInventado.Ok);
        Assert.AreEqual("EmAndamento", (await empresa.GetAsync($"/api/alugueis/{aluguel}")).Propriedade("status").GetString(), "as recusadas não concluem o aluguel");
    }

    [TestMethod]
    public async Task Devolucao_Lista_MostraCondutorClienteEPlaca_E_PorAluguelEPorIdTambem()
    {
        using var empresa = await NovaEmpresa();
        var (aluguel, cenario) = await AluguelEmAndamentoAsync(empresa);
        var criada = await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7));
        var placa = (await empresa.GetAsync($"/api/automoveis/{cenario.Automoveis[0]}")).Propriedade("placa").GetString();

        var lista = await empresa.GetAsync("/api/devolucoes");
        var porId = await empresa.GetAsync($"/api/devolucoes/{criada.Id}");
        var porAluguel = await empresa.GetAsync($"/api/devolucoes/aluguel/{aluguel}");

        var linha = lista.Registros.Single();
        Assert.AreEqual("Condutor de Teste", linha.GetProperty("condutorNome").GetString(), "regressão: a lista chegou a mostrar N/A");
        Assert.AreEqual("Cliente de Teste", linha.GetProperty("clienteNome").GetString());
        Assert.AreEqual(placa, linha.GetProperty("automovelPlaca").GetString());
        Assert.AreEqual(placa, porId.Propriedade("automovelPlaca").GetString());
        Assert.AreEqual(aluguel, porAluguel.Propriedade("aluguelId").GetGuid());
    }

    [TestMethod]
    public async Task Devolucao_Excluir_ReabreOAluguel_ECarroContinuaIndisponivelNoPeriodo()
    {
        using var empresa = await NovaEmpresa();
        var (aluguel, _) = await AluguelEmAndamentoAsync(empresa);
        var devolucao = (await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7))).Id;

        var exclusao = await empresa.DeleteAsync($"/api/devolucoes/{devolucao}");

        Assert.IsTrue(exclusao.Ok, exclusao.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/devolucoes/{devolucao}")).Status);
        Assert.AreEqual("EmAndamento", (await empresa.GetAsync($"/api/alugueis/{aluguel}")).Propriedade("status").GetString(), "desfazer a devolução volta o aluguel a Em andamento");
        Assert.IsTrue((await empresa.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(8))).Ok, "e uma nova devolução pode ser registrada");
    }

    [TestMethod]
    public async Task Devolucao_DeOutraEmpresa_EInvisivel_ENaoSeRegistraParaAluguelAlheio()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var (aluguel, _) = await AluguelEmAndamentoAsync(dona);
        var devolucao = (await dona.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7))).Id;
        var (outro, _) = await AluguelEmAndamentoAsync(dona);

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/devolucoes/{devolucao}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/devolucoes/{devolucao}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/devolucoes")).Registros.Count);
        Assert.IsFalse((await intrusa.RegistrarDevolucaoAsync(outro, DateTimeOffset.UtcNow.AddDays(7))).Ok);
        Assert.AreEqual("EmAndamento", (await dona.GetAsync($"/api/alugueis/{outro}")).Propriedade("status").GetString());
    }

    [TestMethod]
    public async Task Funcionario_RegistraDevolucoes_DaEmpresaDele()
    {
        using var dona = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, dona, "Funcionario");
        var (aluguel, _) = await AluguelEmAndamentoAsync(dona);

        var devolucao = await funcionario.RegistrarDevolucaoAsync(aluguel, DateTimeOffset.UtcNow.AddDays(7));

        Assert.AreEqual(HttpStatusCode.Created, devolucao.Status, devolucao.TextoDosErros);
    }
}
