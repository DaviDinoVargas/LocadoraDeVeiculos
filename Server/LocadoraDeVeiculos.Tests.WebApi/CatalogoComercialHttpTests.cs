using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;

namespace LocadoraDeVeiculos.Tests.WebApi;

/// <summary>
/// Catálogo comercial pela API de verdade: taxas e serviços, cupons (com a validação usada na tela), parceiros, desafios de fidelidade e
/// as configurações da empresa (preço do combustível e caução). Cada teste usa uma empresa própria.
/// </summary>
[TestClass]
[TestCategory("Web API - Catálogo comercial")]
public sealed class CatalogoComercialHttpTests
{
    private static ApiFactory _fabrica = null!;

    [ClassInitialize]
    public static void Iniciar(TestContext _) => _fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "100000" });

    [ClassCleanup]
    public static void Encerrar() => _fabrica.Dispose();

    private static Task<EmpresaDeTeste> NovaEmpresa(string cargo = "Empresa") => EmpresaDeTeste.CriarAsync(_fabrica, cargo);

    private static Task<Resposta> CriarParceiroAsync(EmpresaDeTeste e, string? cnpj = null, string nome = "Parceiro Exemplo") =>
        e.PostAsync("/api/parceiros", new { nome, cnpj = cnpj ?? EmpresaDeTeste.Cnpj(), categoria = "Hotel" });

    private static Task<Resposta> CriarDesafioAsync(EmpresaDeTeste e, Guid cupom, int meta = 2, int periodo = 30, string nome = "Cliente fiel") =>
        e.PostAsync("/api/desafios-cupom", new { nome, descricao = "Alugue mais, pague menos", metaQuantidadeAlugueis = meta, periodoDias = periodo, cupomRecompensaId = cupom });

    // ---------------------------------------------------------------- taxas e serviços -------------------------------------------

    [TestMethod]
    public async Task Taxa_Criada_PodeSerLidaListadaEFiltradaPorTipo()
    {
        using var empresa = await NovaEmpresa();
        var gps = await empresa.CriarTaxaAsync("GPS", 50m, "Fixo");
        await empresa.CriarTaxaAsync("Cadeirinha", 20m, "Diario");

        Assert.AreEqual(HttpStatusCode.Created, gps.Status, gps.TextoDosErros);
        var porId = await empresa.GetAsync($"/api/taxas-servicos/{gps.Id}");
        Assert.AreEqual("GPS", porId.Propriedade("nome").GetString());
        Assert.AreEqual(50m, porId.Propriedade("preco").GetDecimal());
        Assert.AreEqual("Fixo", porId.Propriedade("tipoCalculo").GetString());
        Assert.AreEqual(2, (await empresa.GetAsync("/api/taxas-servicos")).Registros.Count);
        var fixas = await empresa.GetAsync("/api/taxas-servicos/tipo/Fixo");
        CollectionAssert.AreEqual(new[] { "GPS" }, fixas.Registros.Select(r => r.GetProperty("nome").GetString()).ToArray());
        Assert.AreEqual(HttpStatusCode.BadRequest, (await empresa.GetAsync("/api/taxas-servicos/tipo/Inventado")).Status);
    }

    [TestMethod]
    public async Task Taxa_ComNomeRepetido_Recebe409_MasOutraEmpresaPodeUsarONome()
    {
        using var a = await NovaEmpresa();
        using var b = await NovaEmpresa();
        await a.CriarTaxaAsync("GPS", 50m);

        var repetida = await a.CriarTaxaAsync("GPS", 60m);
        var daOutra = await b.CriarTaxaAsync("GPS", 60m);

        Assert.AreEqual(HttpStatusCode.Conflict, repetida.Status);
        StringAssert.Contains(repetida.TextoDosErros, "Já existe uma taxa/serviço com este nome");
        Assert.AreEqual(HttpStatusCode.Created, daOutra.Status);
        Assert.AreEqual(1, (await a.GetAsync("/api/taxas-servicos")).Registros.Count);
    }

    [TestMethod]
    [DataRow(0, "Fixo", "preço zero")]
    [DataRow(-10, "Fixo", "preço negativo")]
    public async Task Taxa_ComPrecoInvalido_Recebe400_ENaoGrava(int preco, string tipo, string motivo)
    {
        using var empresa = await NovaEmpresa();

        var resposta = await empresa.CriarTaxaAsync("Taxa inválida", preco, tipo);

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status, motivo);
        StringAssert.Contains(resposta.TextoDosErros, "maior que zero", motivo);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/taxas-servicos")).Registros.Count);
    }

    [TestMethod]
    public async Task Taxa_Editar_AlteraOPreco_EAEdicaoRecusadaNaoMudaNada()
    {
        using var empresa = await NovaEmpresa();
        var gps = (await empresa.CriarTaxaAsync("GPS", 50m)).Id;
        await empresa.CriarTaxaAsync("Cadeirinha", 20m, "Diario");

        var editada = await empresa.PutAsync($"/api/taxas-servicos/{gps}", new { nome = "GPS", preco = 75m, tipoCalculo = "Fixo" });
        var nomeDeOutra = await empresa.PutAsync($"/api/taxas-servicos/{gps}", new { nome = "Cadeirinha", preco = 75m, tipoCalculo = "Fixo" });

        Assert.IsTrue(editada.Ok, editada.TextoDosErros);
        Assert.AreEqual(75m, (await empresa.GetAsync($"/api/taxas-servicos/{gps}")).Propriedade("preco").GetDecimal());
        Assert.AreEqual(HttpStatusCode.Conflict, nomeDeOutra.Status);
        Assert.AreEqual("GPS", (await empresa.GetAsync($"/api/taxas-servicos/{gps}")).Propriedade("nome").GetString());
    }

    [TestMethod]
    public async Task Taxa_VinculadaAAluguelEmAberto_NaoPodeSerEditadaNemExcluida_ELivrePodeSerExcluida()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        var usada = (await empresa.CriarTaxaAsync("GPS", 50m)).Id;
        var livre = (await empresa.CriarTaxaAsync("Cadeirinha", 20m)).Id;
        Assert.AreEqual(HttpStatusCode.Created, (await empresa.CriarAluguelAsync(cenario, taxas: [usada])).Status);

        var edicao = await empresa.PutAsync($"/api/taxas-servicos/{usada}", new { nome = "GPS", preco = 99m, tipoCalculo = "Fixo" });
        var exclusao = await empresa.DeleteAsync($"/api/taxas-servicos/{usada}");
        var exclusaoLivre = await empresa.DeleteAsync($"/api/taxas-servicos/{livre}");

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, edicao.Status);
        StringAssert.Contains(edicao.TextoDosErros, "vinculada a um aluguel em aberto");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exclusao.Status);
        Assert.AreEqual(50m, (await empresa.GetAsync($"/api/taxas-servicos/{usada}")).Propriedade("preco").GetDecimal(), "o preço contratado não pode mudar");
        Assert.IsTrue(exclusaoLivre.Ok, exclusaoLivre.TextoDosErros);
    }

    [TestMethod]
    public async Task Taxa_FuncionarioConsulta_MasNaoCriaEditaNemExclui_EOutraEmpresaNaoEnxerga()
    {
        using var dona = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, dona, "Funcionario");
        using var intrusa = await NovaEmpresa();
        var taxa = (await dona.CriarTaxaAsync("GPS", 50m)).Id;

        Assert.AreEqual(HttpStatusCode.OK, (await funcionario.GetAsync("/api/taxas-servicos")).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.CriarTaxaAsync("Nova", 10m)).Status, "preço é decisão da gerência");
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.PutAsync($"/api/taxas-servicos/{taxa}", new { nome = "GPS", preco = 1m, tipoCalculo = "Fixo" })).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.DeleteAsync($"/api/taxas-servicos/{taxa}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/taxas-servicos/{taxa}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/taxas-servicos")).Registros.Count);
        Assert.AreEqual(50m, (await dona.GetAsync($"/api/taxas-servicos/{taxa}")).Propriedade("preco").GetDecimal());
    }

    // ---------------------------------------------------------------- cupons -----------------------------------------------------

    [TestMethod]
    public async Task Cupom_Criado_GuardaOCodigoEmMaiusculas_ECodigoRepetidoIgnoraACaixa()
    {
        using var empresa = await NovaEmpresa();

        var criado = await empresa.CriarCupomAsync("bemvindo10", "Percentual", 10m, limiteUsos: 5);
        var repetido = await empresa.CriarCupomAsync("BemVindo10");

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        var cupom = await empresa.GetAsync($"/api/cupons/{criado.Id}");
        Assert.AreEqual("BEMVINDO10", cupom.Propriedade("codigo").GetString());
        Assert.AreEqual(5, cupom.Propriedade("limiteUsos").GetInt32());
        Assert.AreEqual(0, cupom.Propriedade("usosAtuais").GetInt32());
        Assert.IsTrue(cupom.Propriedade("ativo").GetBoolean());
        Assert.AreEqual(HttpStatusCode.Conflict, repetido.Status);
        StringAssert.Contains(repetido.TextoDosErros, "Já existe um cupom com este código");
    }

    [TestMethod]
    public async Task Cupom_ComDadosInvalidos_Recebe400ComAMensagemDeCadaRegra()
    {
        using var empresa = await NovaEmpresa();

        var codigoRuim = await empresa.CriarCupomAsync("com espaço!");
        var percentualAcimaDe100 = await empresa.CriarCupomAsync("MUITO", "Percentual", 150m);
        var valorZero = await empresa.CriarCupomAsync("ZERO", "ValorFixo", 0m);
        var limiteZero = await empresa.CriarCupomAsync("LIMITE0", limiteUsos: 0);
        var vencido = await empresa.CriarCupomAsync("VENCIDO", validoAte: DateTimeOffset.UtcNow.AddDays(-1));

        StringAssert.Contains(codigoRuim.TextoDosErros, "só letras e números");
        StringAssert.Contains(percentualAcimaDe100.TextoDosErros, "não pode ser maior que 100%");
        StringAssert.Contains(valorZero.TextoDosErros, "maior que zero");
        StringAssert.Contains(limiteZero.TextoDosErros, "maior que zero quando informado");
        StringAssert.Contains(vencido.TextoDosErros, "data futura");
        Assert.AreEqual(0, (await empresa.GetAsync("/api/cupons")).Registros.Count, "nenhum cupom inválido pode ter sido gravado");
    }

    [TestMethod]
    public async Task Cupom_DeValorFixo_PodePassarDe100_PoisOLimiteDe100SoValeParaPercentual()
    {
        using var empresa = await NovaEmpresa();

        var resposta = await empresa.CriarCupomAsync("VALE200", "ValorFixo", 200m);

        Assert.AreEqual(HttpStatusCode.Created, resposta.Status, resposta.TextoDosErros);
    }

    [TestMethod]
    public async Task Cupom_NaoPodeSerVinculadoAParceiroDeOutraEmpresa()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var parceiroDaDona = (await CriarParceiroAsync(dona)).Id;

        var resposta = await intrusa.CriarCupomAsync("PARCERIA", parceiroId: parceiroDaDona);

        Assert.IsFalse(resposta.Ok);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/cupons")).Registros.Count);
    }

    [TestMethod]
    public async Task Cupom_Validar_ExplicaPorQueNaoServe_ECalculaODescontoQuandoServe()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        await empresa.CriarCupomAsync("DEZ", "Percentual", 10m);
        await empresa.CriarCupomAsync("UNICO", "ValorFixo", 50m, limiteUsos: 1);
        var desligado = (await empresa.CriarCupomAsync("OFF")).Id;
        await empresa.PutAsync($"/api/cupons/{desligado}", new
        {
            codigo = "OFF", descricao = "x", tipoDesconto = "Percentual", valorDesconto = 10m, validoAte = DateTimeOffset.UtcNow.AddDays(30),
            limiteUsos = (int?)null, parceiroId = (Guid?)null, ativo = false
        });
        await empresa.CriarAluguelAsync(cenario, cupom: "UNICO");      // gasta o único uso

        async Task<Resposta> Validar(string codigo, decimal valor) => await empresa.PostAsync("/api/cupons/validar", new { codigo, valorBase = valor });

        var valido = await Validar("dez", 300m);
        var naoExiste = await Validar("NAOEXISTE", 300m);
        var inativo = await Validar("OFF", 300m);
        var esgotado = await Validar("UNICO", 300m);

        Assert.IsTrue(valido.Propriedade("valido").GetBoolean());
        Assert.AreEqual(30m, valido.Propriedade("valorDesconto").GetDecimal());
        Assert.AreEqual(270m, valido.Propriedade("valorComDesconto").GetDecimal());
        Assert.IsFalse(naoExiste.Propriedade("valido").GetBoolean());
        Assert.AreEqual("Cupom não encontrado.", naoExiste.Propriedade("motivoInvalido").GetString());
        Assert.AreEqual("Cupom inativo.", inativo.Propriedade("motivoInvalido").GetString());
        Assert.AreEqual("Cupom esgotou o limite de usos.", esgotado.Propriedade("motivoInvalido").GetString());
        Assert.AreEqual(300m, esgotado.Propriedade("valorComDesconto").GetDecimal(), "cupom inválido não desconta nada");
    }

    [TestMethod]
    public async Task Cupom_EmUsoPorAluguelEmAberto_NaoPodeSerEditadoNemExcluido_ESemUsoPodeSerExcluido()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        var usado = (await empresa.CriarCupomAsync("EMUSO")).Id;
        var livre = (await empresa.CriarCupomAsync("LIVRE")).Id;
        Assert.AreEqual(HttpStatusCode.Created, (await empresa.CriarAluguelAsync(cenario, cupom: "EMUSO")).Status);

        var edicao = await empresa.PutAsync($"/api/cupons/{usado}", new
        {
            codigo = "EMUSO", descricao = "x", tipoDesconto = "Percentual", valorDesconto = 50m, validoAte = DateTimeOffset.UtcNow.AddDays(30),
            limiteUsos = (int?)null, parceiroId = (Guid?)null, ativo = true
        });
        var exclusao = await empresa.DeleteAsync($"/api/cupons/{usado}");
        var exclusaoLivre = await empresa.DeleteAsync($"/api/cupons/{livre}");

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, edicao.Status);
        StringAssert.Contains(edicao.TextoDosErros, "em uso por um aluguel em aberto");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exclusao.Status);
        Assert.AreEqual(10m, (await empresa.GetAsync($"/api/cupons/{usado}")).Propriedade("valorDesconto").GetDecimal(), "o desconto contratado não pode mudar");
        Assert.IsTrue(exclusaoLivre.Ok, exclusaoLivre.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/cupons/{livre}")).Status);
    }

    [TestMethod]
    public async Task Cupom_Editar_AceitaManterOCodigo_MasNaoTomarODeOutroCupom_ECupomDeOutraEmpresaEInvisivel()
    {
        using var empresa = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var a = (await empresa.CriarCupomAsync("CUPOMA")).Id;
        await empresa.CriarCupomAsync("CUPOMB");

        object Corpo(string codigo) => new
        {
            codigo, descricao = "editado", tipoDesconto = "Percentual", valorDesconto = 15m, validoAte = DateTimeOffset.UtcNow.AddDays(30),
            limiteUsos = (int?)null, parceiroId = (Guid?)null, ativo = true
        };

        var manter = await empresa.PutAsync($"/api/cupons/{a}", Corpo("CUPOMA"));
        var tomar = await empresa.PutAsync($"/api/cupons/{a}", Corpo("CUPOMB"));

        Assert.IsTrue(manter.Ok, manter.TextoDosErros);
        Assert.AreEqual(15m, (await empresa.GetAsync($"/api/cupons/{a}")).Propriedade("valorDesconto").GetDecimal());
        Assert.AreEqual(HttpStatusCode.Conflict, tomar.Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/cupons/{a}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/cupons")).Registros.Count);
    }

    [TestMethod]
    public async Task Cupom_FuncionarioConsultaEValida_MasNaoCriaEdita()
    {
        using var dona = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, dona, "Funcionario");
        var cupom = (await dona.CriarCupomAsync("DEZ")).Id;

        Assert.AreEqual(HttpStatusCode.OK, (await funcionario.GetAsync("/api/cupons")).Status);
        Assert.IsTrue((await funcionario.PostAsync("/api/cupons/validar", new { codigo = "DEZ", valorBase = 100m })).Propriedade("valido").GetBoolean());
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.CriarCupomAsync("NOVO")).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.DeleteAsync($"/api/cupons/{cupom}")).Status);
    }

    // ---------------------------------------------------------------- parceiros ---------------------------------------------------

    [TestMethod]
    public async Task Parceiro_Criado_PodeSerLidoEditadoEExcluido()
    {
        using var empresa = await NovaEmpresa();
        var cnpj = EmpresaDeTeste.Cnpj();

        var criado = await CriarParceiroAsync(empresa, cnpj, "Hotel Central");
        var editado = await empresa.PutAsync($"/api/parceiros/{criado.Id}", new { nome = "Hotel Central Premium", cnpj, categoria = "Hotel", ativo = false });

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        Assert.IsTrue(editado.Ok, editado.TextoDosErros);
        var lido = await empresa.GetAsync($"/api/parceiros/{criado.Id}");
        Assert.AreEqual("Hotel Central Premium", lido.Propriedade("nome").GetString());
        Assert.IsFalse(lido.Propriedade("ativo").GetBoolean());
        Assert.AreEqual(1, (await empresa.GetAsync("/api/parceiros")).Registros.Count);
        Assert.IsTrue((await empresa.DeleteAsync($"/api/parceiros/{criado.Id}")).Ok);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/parceiros/{criado.Id}")).Status);
    }

    [TestMethod]
    public async Task Parceiro_ComCnpjRepetidoOuInvalido_EhRecusado_ECnpjPodeRepetirEmOutraEmpresa()
    {
        using var a = await NovaEmpresa();
        using var b = await NovaEmpresa();
        var cnpj = EmpresaDeTeste.Cnpj();
        await CriarParceiroAsync(a, cnpj);

        var repetido = await CriarParceiroAsync(a, cnpj);
        var invalido = await CriarParceiroAsync(a, "11.111.111/1111-11");
        var daOutra = await CriarParceiroAsync(b, cnpj);

        Assert.AreEqual(HttpStatusCode.Conflict, repetido.Status);
        StringAssert.Contains(repetido.TextoDosErros, "Já existe um parceiro com este CNPJ");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalido.Status);
        Assert.AreEqual(HttpStatusCode.Created, daOutra.Status);
        Assert.AreEqual(1, (await a.GetAsync("/api/parceiros")).Registros.Count);
    }

    [TestMethod]
    public async Task Parceiro_ComCuponsVinculados_NaoPodeSerExcluido_ADicaEDesativar()
    {
        using var empresa = await NovaEmpresa();
        var parceiro = (await CriarParceiroAsync(empresa)).Id;
        await empresa.CriarCupomAsync("PARCERIA", parceiroId: parceiro);

        var exclusao = await empresa.DeleteAsync($"/api/parceiros/{parceiro}");

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exclusao.Status);
        StringAssert.Contains(exclusao.TextoDosErros, "cupons vinculados");
        StringAssert.Contains(exclusao.TextoDosErros, "Desative-o");
        Assert.AreEqual(HttpStatusCode.OK, (await empresa.GetAsync($"/api/parceiros/{parceiro}")).Status);
        Assert.AreEqual("Parceiro Exemplo", (await empresa.GetAsync($"/api/cupons/{(await empresa.GetAsync("/api/cupons")).Registros[0].GetProperty("id").GetGuid()}")).Propriedade("parceiroNome").GetString());
    }

    [TestMethod]
    public async Task Parceiro_DeOutraEmpresa_EInvisivel()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var parceiro = (await CriarParceiroAsync(dona)).Id;

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/parceiros/{parceiro}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/parceiros/{parceiro}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/parceiros")).Registros.Count);
    }

    // ---------------------------------------------------------------- desafios ----------------------------------------------------

    [TestMethod]
    public async Task Desafio_Criado_MostraOCodigoDoCupomDeRecompensa()
    {
        using var empresa = await NovaEmpresa();
        var cupom = (await empresa.CriarCupomAsync("PREMIO")).Id;

        var criado = await CriarDesafioAsync(empresa, cupom, meta: 3, periodo: 60);

        Assert.AreEqual(HttpStatusCode.Created, criado.Status, criado.TextoDosErros);
        var lido = await empresa.GetAsync($"/api/desafios-cupom/{criado.Id}");
        Assert.AreEqual(3, lido.Propriedade("metaQuantidadeAlugueis").GetInt32());
        Assert.AreEqual(60, lido.Propriedade("periodoDias").GetInt32());
        Assert.AreEqual("PREMIO", lido.Propriedade("cupomRecompensaCodigo").GetString());
        Assert.AreEqual("PREMIO", (await empresa.GetAsync("/api/desafios-cupom")).Registros[0].GetProperty("cupomRecompensaCodigo").GetString());
    }

    [TestMethod]
    public async Task Desafio_ComMetaOuPeriodoZero_OuCupomInexistente_EhRecusado()
    {
        using var empresa = await NovaEmpresa();
        var cupom = (await empresa.CriarCupomAsync("PREMIO")).Id;

        var semMeta = await CriarDesafioAsync(empresa, cupom, meta: 0);
        var semPeriodo = await CriarDesafioAsync(empresa, cupom, periodo: 0);
        var cupomFantasma = await CriarDesafioAsync(empresa, Guid.NewGuid());

        Assert.AreEqual(HttpStatusCode.BadRequest, semMeta.Status);
        Assert.AreEqual(HttpStatusCode.BadRequest, semPeriodo.Status);
        Assert.AreEqual(HttpStatusCode.NotFound, cupomFantasma.Status);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/desafios-cupom")).Registros.Count);
    }

    [TestMethod]
    public async Task Desafio_NaoPodeUsarComoRecompensaOCupomDeOutraEmpresa()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var cupomDaDona = (await dona.CriarCupomAsync("PREMIO")).Id;

        var resposta = await CriarDesafioAsync(intrusa, cupomDaDona);

        Assert.IsFalse(resposta.Ok);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/desafios-cupom")).Registros.Count);
    }

    [TestMethod]
    public async Task Desafio_Progresso_ContaAlugueisConcluidosDoCliente_ELiberaOCupomAoBaterAMeta()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        var cupom = (await empresa.CriarCupomAsync("FIEL10")).Id;
        await CriarDesafioAsync(empresa, cupom, meta: 2);
        var desligado = (await CriarDesafioAsync(empresa, cupom, meta: 1, nome: "Desligado")).Id;
        await empresa.PutAsync($"/api/desafios-cupom/{desligado}", new
        {
            nome = "Desligado", descricao = "x", metaQuantidadeAlugueis = 1, periodoDias = 30, cupomRecompensaId = cupom, ativo = false
        });

        async Task<System.Text.Json.JsonElement> Progresso() => (await empresa.GetAsync($"/api/desafios-cupom/cliente/{cenario.Cliente}/progresso")).Propriedade("progresso").EnumerateArray().Single();

        Assert.AreEqual(0, (await Progresso()).GetProperty("progresso").GetInt32(), "ainda sem aluguéis concluídos");

        var primeiro = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[0])).Id;
        await empresa.IniciarAluguelAsync(primeiro);
        await empresa.RegistrarDevolucaoAsync(primeiro, DateTimeOffset.UtcNow.AddDays(9));
        var meio = await Progresso();

        var segundo = (await empresa.CriarAluguelAsync(cenario, cenario.Automoveis[1])).Id;
        await empresa.IniciarAluguelAsync(segundo);
        await empresa.RegistrarDevolucaoAsync(segundo, DateTimeOffset.UtcNow.AddDays(9));
        var fim = await Progresso();

        Assert.AreEqual(1, meio.GetProperty("progresso").GetInt32());
        Assert.IsFalse(meio.GetProperty("cumprido").GetBoolean());
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, meio.GetProperty("cupomLiberadoCodigo").ValueKind, "o cupom só é liberado ao bater a meta");
        Assert.AreEqual(2, fim.GetProperty("progresso").GetInt32());
        Assert.IsTrue(fim.GetProperty("cumprido").GetBoolean());
        Assert.AreEqual("FIEL10", fim.GetProperty("cupomLiberadoCodigo").GetString());
        Assert.AreEqual("Cliente fiel", fim.GetProperty("nome").GetString(), "o desafio desligado não aparece no progresso");
    }

    [TestMethod]
    public async Task Desafio_Editar_ExcluirEFuncionario_SomenteEmpresaEscreve()
    {
        using var dona = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, dona, "Funcionario");
        using var intrusa = await NovaEmpresa();
        var cupom = (await dona.CriarCupomAsync("PREMIO")).Id;
        var desafio = (await CriarDesafioAsync(dona, cupom)).Id;

        var editado = await dona.PutAsync($"/api/desafios-cupom/{desafio}", new
        {
            nome = "Novo nome", descricao = "x", metaQuantidadeAlugueis = 5, periodoDias = 90, cupomRecompensaId = cupom, ativo = true
        });

        Assert.IsTrue(editado.Ok, editado.TextoDosErros);
        Assert.AreEqual(5, (await dona.GetAsync($"/api/desafios-cupom/{desafio}")).Propriedade("metaQuantidadeAlugueis").GetInt32());
        Assert.AreEqual(HttpStatusCode.OK, (await funcionario.GetAsync("/api/desafios-cupom")).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await CriarDesafioAsync(funcionario, cupom)).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.DeleteAsync($"/api/desafios-cupom/{desafio}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/desafios-cupom/{desafio}")).Status);
        Assert.IsTrue((await dona.DeleteAsync($"/api/desafios-cupom/{desafio}")).Ok);
        Assert.AreEqual(HttpStatusCode.NotFound, (await dona.GetAsync($"/api/desafios-cupom/{desafio}")).Status);
    }

    [TestMethod]
    public async Task Cupom_UsadoComoRecompensaDeDesafio_NaoPodeSerExcluido()
    {
        using var empresa = await NovaEmpresa();
        var cupom = (await empresa.CriarCupomAsync("PREMIO")).Id;
        var desafio = (await CriarDesafioAsync(empresa, cupom)).Id;

        var exclusao = await empresa.DeleteAsync($"/api/cupons/{cupom}");

        Assert.IsFalse(exclusao.Ok, "excluir o cupom deixaria o desafio prometendo um prêmio que não existe mais");
        Assert.AreEqual("PREMIO", (await empresa.GetAsync($"/api/desafios-cupom/{desafio}")).Propriedade("cupomRecompensaCodigo").GetString());
    }

    // ---------------------------------------------------------------- configurações -----------------------------------------------

    [TestMethod]
    public async Task Configuracao_SemNadaGravado_DevolveOsValoresPadrao_EOsAjustesFicamGravados()
    {
        using var empresa = await NovaEmpresa();

        var padrao = await empresa.GetAsync("/api/configuracoes");
        await empresa.DefinirPrecoDoCombustivelAsync(6.25m);
        await empresa.DefinirValorDaGarantiaAsync(1500m);
        var ajustada = await empresa.GetAsync("/api/configuracoes");

        Assert.AreEqual(5.50m, padrao.Propriedade("precoCombustivel").GetDecimal());
        Assert.AreEqual(1000m, padrao.Propriedade("valorGarantia").GetDecimal());
        Assert.AreEqual(6.25m, ajustada.Propriedade("precoCombustivel").GetDecimal());
        Assert.AreEqual(1500m, ajustada.Propriedade("valorGarantia").GetDecimal(), "ajustar o combustível não apaga a caução e vice-versa");
    }

    [TestMethod]
    public async Task Configuracao_ComValoresInvalidos_Recebe400_EOValorAnteriorFica()
    {
        using var empresa = await NovaEmpresa();
        await empresa.DefinirPrecoDoCombustivelAsync(6m);
        await empresa.DefinirValorDaGarantiaAsync(800m);

        var combustivelZero = await empresa.DefinirPrecoDoCombustivelAsync(0m);
        var garantiaNegativa = await empresa.DefinirValorDaGarantiaAsync(-1m);
        var garantiaAbsurda = await empresa.DefinirValorDaGarantiaAsync(100_000.01m);

        Assert.AreEqual(HttpStatusCode.BadRequest, combustivelZero.Status);
        StringAssert.Contains(combustivelZero.TextoDosErros, "maior que zero");
        StringAssert.Contains(garantiaNegativa.TextoDosErros, "não pode ser negativo");
        StringAssert.Contains(garantiaAbsurda.TextoDosErros, "não pode passar de R$ 100.000,00");
        var atual = await empresa.GetAsync("/api/configuracoes");
        Assert.AreEqual(6m, atual.Propriedade("precoCombustivel").GetDecimal());
        Assert.AreEqual(800m, atual.Propriedade("valorGarantia").GetDecimal());
    }

    [TestMethod]
    public async Task Configuracao_EDaEmpresa_UmaEmpresaNaoMudaAdaOutra_EFuncionarioNaoAcessa()
    {
        using var a = await NovaEmpresa();
        using var b = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, a, "Funcionario");

        await a.DefinirPrecoDoCombustivelAsync(9m);

        Assert.AreEqual(5.50m, (await b.GetAsync("/api/configuracoes")).Propriedade("precoCombustivel").GetDecimal(), "a outra empresa continua com o padrão");
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.GetAsync("/api/configuracoes")).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.DefinirPrecoDoCombustivelAsync(1m)).Status);
        Assert.AreEqual(9m, (await a.GetAsync("/api/configuracoes")).Propriedade("precoCombustivel").GetDecimal());
    }

    [TestMethod]
    public async Task Cupom_Validar_CupomVencidoDizExpirado_ECupomInvalidoNuncaVemComoValido()
    {
        using var empresa = await NovaEmpresa();
        await empresa.CriarCupomAsync("CURTO", validoAte: DateTimeOffset.UtcNow.AddMilliseconds(300));
        await Task.Delay(500);

        var resposta = await empresa.PostAsync("/api/cupons/validar", new { codigo = "CURTO", valorBase = 200m });

        Assert.IsFalse(resposta.Propriedade("valido").GetBoolean());
        Assert.AreEqual("Cupom expirado.", resposta.Propriedade("motivoInvalido").GetString());
        Assert.AreEqual(0m, resposta.Propriedade("valorDesconto").GetDecimal());
        Assert.AreEqual(200m, resposta.Propriedade("valorComDesconto").GetDecimal());
    }

    [TestMethod]
    public async Task Cupom_Exclusao_BloqueadaPorAluguelOuPorDesafio_ExplicaOMotivoDeCada()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        var emAluguel = (await empresa.CriarCupomAsync("NOALUGUEL")).Id;
        var emDesafio = (await empresa.CriarCupomAsync("NODESAFIO")).Id;
        await empresa.CriarAluguelAsync(cenario, cupom: "NOALUGUEL");
        await CriarDesafioAsync(empresa, emDesafio);

        var porAluguel = await empresa.DeleteAsync($"/api/cupons/{emAluguel}");
        var porDesafio = await empresa.DeleteAsync($"/api/cupons/{emDesafio}");

        StringAssert.Contains(porAluguel.TextoDosErros, "em uso por um aluguel em aberto");
        StringAssert.Contains(porDesafio.TextoDosErros, "recompensa de um desafio");
        StringAssert.Contains(porDesafio.TextoDosErros, "Exclua ou troque o desafio");
    }

    [TestMethod]
    public async Task Cupom_Editar_CodigoDeOutroCupom_Recebe409ComMensagem()
    {
        using var empresa = await NovaEmpresa();
        var a = (await empresa.CriarCupomAsync("CUPOMA")).Id;
        await empresa.CriarCupomAsync("CUPOMB");

        var tomar = await empresa.PutAsync($"/api/cupons/{a}", new
        {
            codigo = "cupomb", descricao = "x", tipoDesconto = "Percentual", valorDesconto = 10m, validoAte = DateTimeOffset.UtcNow.AddDays(30),
            limiteUsos = (int?)null, parceiroId = (Guid?)null, ativo = true
        });

        Assert.AreEqual(HttpStatusCode.Conflict, tomar.Status);
        StringAssert.Contains(tomar.TextoDosErros, "Já existe um cupom com este código");
    }

    [TestMethod]
    public async Task Taxa_Mensagens_DeNomeRepetidoNaEdicaoEDeExclusaoBloqueada()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        var a = (await empresa.CriarTaxaAsync("GPS", 50m)).Id;
        await empresa.CriarTaxaAsync("Cadeirinha", 20m);
        var usada = (await empresa.CriarTaxaAsync("Capa de banco", 15m)).Id;
        await empresa.CriarAluguelAsync(cenario, taxas: [usada]);

        var nomeRepetido = await empresa.PutAsync($"/api/taxas-servicos/{a}", new { nome = "Cadeirinha", preco = 50m, tipoCalculo = "Fixo" });
        var exclusao = await empresa.DeleteAsync($"/api/taxas-servicos/{usada}");

        StringAssert.Contains(nomeRepetido.TextoDosErros, "Já existe uma taxa/serviço com este nome");
        StringAssert.Contains(exclusao.TextoDosErros, "excluir uma taxa/serviço vinculada a um aluguel em aberto");
        Assert.AreEqual(HttpStatusCode.OK, (await empresa.GetAsync($"/api/taxas-servicos/{usada}")).Status, "a taxa bloqueada continua cadastrada");
    }
}
