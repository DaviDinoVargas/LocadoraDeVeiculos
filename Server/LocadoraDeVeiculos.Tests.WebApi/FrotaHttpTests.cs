using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;

namespace LocadoraDeVeiculos.Tests.WebApi;

/// <summary>
/// Frota (grupos, planos de cobrança e automóveis) pela API de verdade: o que o usuário consegue ou não fazer, o que a API
/// responde e o que fica gravado. Cada teste usa uma empresa própria, então também prova o isolamento entre empresas.
/// </summary>
[TestClass]
[TestCategory("Web API - Frota")]
public sealed class FrotaHttpTests
{
    private static ApiFactory _fabrica = null!;

    [ClassInitialize]
    public static void Iniciar(TestContext _) => _fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "100000" });

    [ClassCleanup]
    public static void Encerrar() => _fabrica.Dispose();

    private static Task<EmpresaDeTeste> NovaEmpresa(string cargo = "Empresa") => EmpresaDeTeste.CriarAsync(_fabrica, cargo);

    // ---------------------------------------------------------------- grupos de automóvel ----------------------------------

    [TestMethod]
    public async Task Grupo_Criado_PodeSerLidoPorIdENaLista()
    {
        using var empresa = await NovaEmpresa();

        var criado = await empresa.PostAsync("/api/grupos-automovel", new { nome = "SUV", descricao = "Utilitários" });

        Assert.AreEqual(HttpStatusCode.Created, criado.Status);
        var porId = await empresa.GetAsync($"/api/grupos-automovel/{criado.Id}");
        Assert.AreEqual("SUV", porId.Propriedade("nome").GetString());
        Assert.AreEqual("Utilitários", porId.Propriedade("descricao").GetString());
        var lista = await empresa.GetAsync("/api/grupos-automovel");
        CollectionAssert.AreEqual(new[] { "SUV" }, lista.Registros.Select(r => r.GetProperty("nome").GetString()).ToArray());
    }

    [TestMethod]
    public async Task Grupo_ComNomeRepetido_Recebe409_MasOutraEmpresaPodeUsarOMesmoNome()
    {
        using var empresaA = await NovaEmpresa();
        using var empresaB = await NovaEmpresa();
        await empresaA.PostAsync("/api/grupos-automovel", new { nome = "Econômico", descricao = (string?)null });

        var repetido = await empresaA.PostAsync("/api/grupos-automovel", new { nome = "Econômico", descricao = (string?)null });
        var outraEmpresa = await empresaB.PostAsync("/api/grupos-automovel", new { nome = "Econômico", descricao = (string?)null });

        Assert.AreEqual(HttpStatusCode.Conflict, repetido.Status);
        StringAssert.Contains(repetido.TextoDosErros, "Já existe um grupo de automóvel com este nome");
        Assert.AreEqual(HttpStatusCode.Created, outraEmpresa.Status, "o nome só precisa ser único dentro da empresa");
    }

    [TestMethod]
    public async Task Grupo_Editar_AceitaManterOProprioNome_MasNaoTomarOdeOutroGrupo()
    {
        using var empresa = await NovaEmpresa();
        var a = (await empresa.CriarGrupoAsync("Grupo A")).Id;
        await empresa.CriarGrupoAsync("Grupo B");

        var mesmoNome = await empresa.PutAsync($"/api/grupos-automovel/{a}", new { nome = "Grupo A", descricao = "nova descrição" });
        var nomeDeOutro = await empresa.PutAsync($"/api/grupos-automovel/{a}", new { nome = "Grupo B", descricao = (string?)null });

        Assert.IsTrue(mesmoNome.Ok, mesmoNome.TextoDosErros);
        Assert.AreEqual("nova descrição", (await empresa.GetAsync($"/api/grupos-automovel/{a}")).Propriedade("descricao").GetString());
        Assert.AreEqual(HttpStatusCode.Conflict, nomeDeOutro.Status);
        Assert.AreEqual("Grupo A", (await empresa.GetAsync($"/api/grupos-automovel/{a}")).Propriedade("nome").GetString(), "a edição recusada não pode alterar nada");
    }

    [TestMethod]
    public async Task Grupo_EmUsoPorAutomovelOuPlano_NaoPodeSerExcluido_EVazioPode()
    {
        using var empresa = await NovaEmpresa();
        var comCarro = (await empresa.CriarGrupoAsync()).Id;
        await empresa.CriarAutomovelAsync(comCarro);
        var comPlano = (await empresa.CriarGrupoAsync()).Id;
        await empresa.CriarPlanoAsync(comPlano);
        var vazio = (await empresa.CriarGrupoAsync()).Id;

        var porCarro = await empresa.DeleteAsync($"/api/grupos-automovel/{comCarro}");
        var porPlano = await empresa.DeleteAsync($"/api/grupos-automovel/{comPlano}");
        var semUso = await empresa.DeleteAsync($"/api/grupos-automovel/{vazio}");

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, porCarro.Status);
        StringAssert.Contains(porCarro.TextoDosErros, "utilizado em automóveis");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, porPlano.Status);
        StringAssert.Contains(porPlano.TextoDosErros, "planos de cobrança");
        Assert.IsTrue(semUso.Ok, semUso.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/grupos-automovel/{vazio}")).Status);
        Assert.AreEqual(HttpStatusCode.OK, (await empresa.GetAsync($"/api/grupos-automovel/{comCarro}")).Status, "o grupo bloqueado continua existindo");
    }

    [TestMethod]
    public async Task Grupo_DeOutraEmpresa_EInvisivel_NaoPodeSerLidoEditadoNemExcluido()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var grupo = (await dona.CriarGrupoAsync("Só da dona")).Id;

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/grupos-automovel/{grupo}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.PutAsync($"/api/grupos-automovel/{grupo}", new { nome = "Invadido", descricao = (string?)null })).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/grupos-automovel/{grupo}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/grupos-automovel")).Registros.Count);
        Assert.AreEqual("Só da dona", (await dona.GetAsync($"/api/grupos-automovel/{grupo}")).Propriedade("nome").GetString(), "a tentativa não pode ter alterado o grupo");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("ab")]
    public async Task Grupo_ComNomeInvalido_Recebe400(string nome)
    {
        using var empresa = await NovaEmpresa();

        var resposta = await empresa.PostAsync("/api/grupos-automovel", new { nome, descricao = (string?)null });

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status);
        Assert.IsTrue(resposta.Erros.Count > 0);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/grupos-automovel")).Registros.Count, "nada pode ter sido gravado");
    }

    // ---------------------------------------------------------------- planos de cobrança ------------------------------------

    [TestMethod]
    public async Task Plano_Criado_FicaVinculadoAoGrupoComOsValoresInformados()
    {
        using var empresa = await NovaEmpresa();
        var grupo = (await empresa.CriarGrupoAsync()).Id;

        var criado = await empresa.CriarPlanoAsync(grupo, diaria: 150m, porKm: 2.5m, kmLivre: 200);

        Assert.AreEqual(HttpStatusCode.Created, criado.Status);
        var plano = await empresa.GetAsync($"/api/planos-cobranca/{criado.Id}");
        Assert.AreEqual(150m, plano.Propriedade("precoDiaria").GetDecimal());
        Assert.AreEqual(2.5m, plano.Propriedade("precoPorKm").GetDecimal());
        Assert.AreEqual(200, plano.Propriedade("kmLivreLimite").GetInt32());
        Assert.AreEqual(grupo, plano.Propriedade("grupoAutomovelId").GetGuid());
    }

    [TestMethod]
    [DataRow(0, 2, 100, "diária zero")]
    [DataRow(-5, 2, 100, "diária negativa")]
    [DataRow(100, -1, 100, "preço por km negativo")]
    [DataRow(100, 2, -1, "km livre negativo")]
    public async Task Plano_ComValorInvalido_Recebe400_ENaoGrava(int diaria, int porKm, int kmLivre, string motivo)
    {
        using var empresa = await NovaEmpresa();
        var grupo = (await empresa.CriarGrupoAsync()).Id;

        var resposta = await empresa.CriarPlanoAsync(grupo, diaria, porKm, kmLivre);

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status, motivo);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/planos-cobranca")).Registros.Count, motivo);
    }

    [TestMethod]
    public async Task Plano_Editar_AlteraOPrecoEPlanoExcluidoSome()
    {
        using var empresa = await NovaEmpresa();
        var grupo = (await empresa.CriarGrupoAsync()).Id;
        var plano = (await empresa.CriarPlanoAsync(grupo, 100m)).Id;

        var editado = await empresa.PutAsync($"/api/planos-cobranca/{plano}",
            new { grupoAutomovelId = grupo, nome = "Plano premium", precoDiaria = 180m, precoPorKm = 3m, kmLivreLimite = 50 });

        Assert.IsTrue(editado.Ok, editado.TextoDosErros);
        var lido = await empresa.GetAsync($"/api/planos-cobranca/{plano}");
        Assert.AreEqual(180m, lido.Propriedade("precoDiaria").GetDecimal());
        Assert.AreEqual("Plano premium", lido.Propriedade("nome").GetString());
        Assert.IsTrue((await empresa.DeleteAsync($"/api/planos-cobranca/{plano}")).Ok);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/planos-cobranca/{plano}")).Status);
    }

    [TestMethod]
    public async Task Plano_DeOutraEmpresa_EInvisivel()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var plano = (await dona.CriarPlanoAsync((await dona.CriarGrupoAsync()).Id)).Id;

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/planos-cobranca/{plano}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/planos-cobranca/{plano}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/planos-cobranca")).Registros.Count);
    }

    // ---------------------------------------------------------------- automóveis ---------------------------------------------

    [TestMethod]
    public async Task Automovel_Criado_AparecePorIdNaListaEPorGrupo()
    {
        using var empresa = await NovaEmpresa();
        var grupo = (await empresa.CriarGrupoAsync()).Id;
        var outroGrupo = (await empresa.CriarGrupoAsync()).Id;

        var criado = await empresa.CriarAutomovelAsync(grupo, "ABC1D23");
        await empresa.CriarAutomovelAsync(outroGrupo, "XYZ9Z99");

        Assert.AreEqual(HttpStatusCode.Created, criado.Status);
        var porId = await empresa.GetAsync($"/api/automoveis/{criado.Id}");
        Assert.AreEqual("ABC1D23", porId.Propriedade("placa").GetString());
        Assert.AreEqual(2, (await empresa.GetAsync("/api/automoveis")).Registros.Count);
        var doGrupo = await empresa.GetAsync($"/api/automoveis/grupo/{grupo}");
        CollectionAssert.AreEqual(new[] { "ABC1D23" }, doGrupo.Registros.Select(r => r.GetProperty("placa").GetString()).ToArray());
    }

    [TestMethod]
    public async Task Automovel_ComPlacaRepetida_Recebe409_MasOutraEmpresaPodeCadastrarAMesmaPlaca()
    {
        using var empresaA = await NovaEmpresa();
        using var empresaB = await NovaEmpresa();
        var grupoA = (await empresaA.CriarGrupoAsync()).Id;
        var grupoB = (await empresaB.CriarGrupoAsync()).Id;
        await empresaA.CriarAutomovelAsync(grupoA, "ABC1D23");

        var repetida = await empresaA.CriarAutomovelAsync(grupoA, "ABC1D23");
        var daOutraEmpresa = await empresaB.CriarAutomovelAsync(grupoB, "ABC1D23");

        Assert.AreEqual(HttpStatusCode.Conflict, repetida.Status);
        StringAssert.Contains(repetida.TextoDosErros, "Já existe um automóvel com esta placa");
        Assert.AreEqual(HttpStatusCode.Created, daOutraEmpresa.Status);
        Assert.AreEqual(1, (await empresaA.GetAsync("/api/automoveis")).Registros.Count);
    }

    [TestMethod]
    [DataRow("abc1d23", "formato")]
    [DataRow("ABC123", "tamanho")]
    [DataRow("1BC1D23", "começa com número")]
    public async Task Automovel_ComPlacaFora_DoPadrao_Recebe400(string placa, string motivo)
    {
        using var empresa = await NovaEmpresa();
        var grupo = (await empresa.CriarGrupoAsync()).Id;

        var resposta = await empresa.CriarAutomovelAsync(grupo, placa);

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status, motivo);
        StringAssert.Contains(resposta.TextoDosErros, "Placa", motivo);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/automoveis")).Registros.Count);
    }

    [TestMethod]
    public async Task Automovel_ComDadosInvalidos_Recebe400ComUmaMensagemPorProblema()
    {
        using var empresa = await NovaEmpresa();
        var grupo = (await empresa.CriarGrupoAsync()).Id;

        var resposta = await empresa.PostAsync("/api/automoveis", new
        {
            placa = "ABC1D23", marca = "F", cor = "Pr", modelo = "U", tipoCombustivel = "Gasolina",
            capacidadeTanque = 0m, ano = 1800, foto = (string?)null, grupoAutomovelId = grupo
        });

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status);
        var texto = resposta.TextoDosErros;
        StringAssert.Contains(texto, "Marca");
        StringAssert.Contains(texto, "Cor");
        StringAssert.Contains(texto, "Capacidade");
        StringAssert.Contains(texto, "Ano");
    }

    [TestMethod]
    public async Task Automovel_Editar_TrocaDados_RecusaPlacaDeOutroCarro_EAceitaManterAPropria()
    {
        using var empresa = await NovaEmpresa();
        var grupo = (await empresa.CriarGrupoAsync()).Id;
        var carro = (await empresa.CriarAutomovelAsync(grupo, "ABC1D23")).Id;
        await empresa.CriarAutomovelAsync(grupo, "XYZ9Z99");

        object Corpo(string placa, string cor) => new
        {
            placa, marca = "Fiat", cor, modelo = "Uno", tipoCombustivel = "Gasolina", capacidadeTanque = 50m, ano = 2022,
            foto = (string?)null, grupoAutomovelId = grupo
        };

        var mesmaPlaca = await empresa.PutAsync($"/api/automoveis/{carro}", Corpo("ABC1D23", "Vermelho"));
        var placaDeOutro = await empresa.PutAsync($"/api/automoveis/{carro}", Corpo("XYZ9Z99", "Azul"));

        Assert.IsTrue(mesmaPlaca.Ok, mesmaPlaca.TextoDosErros);
        Assert.AreEqual("Vermelho", (await empresa.GetAsync($"/api/automoveis/{carro}")).Propriedade("cor").GetString());
        Assert.AreEqual(HttpStatusCode.Conflict, placaDeOutro.Status);
        Assert.AreEqual("ABC1D23", (await empresa.GetAsync($"/api/automoveis/{carro}")).Propriedade("placa").GetString());
    }

    [TestMethod]
    public async Task Automovel_ComAluguelEmAberto_NaoPodeSerEditadoNemExcluido()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync();
        var carro = cenario.Automoveis[0];
        var aluguel = await empresa.PostAsync("/api/alugueis", EmpresaDeTeste.CorpoDeAluguel(cenario));
        Assert.AreEqual(HttpStatusCode.Created, aluguel.Status, aluguel.TextoDosErros);

        var edicao = await empresa.PutAsync($"/api/automoveis/{carro}", new
        {
            placa = EmpresaDeTeste.Placa(), marca = "Fiat", cor = "Preto", modelo = "Uno", tipoCombustivel = "Gasolina", capacidadeTanque = 50m,
            ano = 2022, foto = (string?)null, grupoAutomovelId = cenario.Grupo
        });
        var exclusao = await empresa.DeleteAsync($"/api/automoveis/{carro}");

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, edicao.Status);
        StringAssert.Contains(edicao.TextoDosErros, "aluguel em aberto");
        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, exclusao.Status);
        Assert.AreEqual(HttpStatusCode.OK, (await empresa.GetAsync($"/api/automoveis/{carro}")).Status, "o carro continua cadastrado");
    }

    [TestMethod]
    public async Task Automovel_SemAluguel_PodeSerExcluido()
    {
        using var empresa = await NovaEmpresa();
        var carro = (await empresa.CriarAutomovelAsync((await empresa.CriarGrupoAsync()).Id)).Id;

        var exclusao = await empresa.DeleteAsync($"/api/automoveis/{carro}");

        Assert.IsTrue(exclusao.Ok, exclusao.TextoDosErros);
        Assert.AreEqual(HttpStatusCode.NotFound, (await empresa.GetAsync($"/api/automoveis/{carro}")).Status);
        Assert.AreEqual(0, (await empresa.GetAsync("/api/automoveis")).Registros.Count);
    }

    [TestMethod]
    public async Task Automovel_DeOutraEmpresa_EInvisivel_NaoPodeSerEditadoNemExcluido()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var grupo = (await dona.CriarGrupoAsync()).Id;
        var carro = (await dona.CriarAutomovelAsync(grupo, "ABC1D23")).Id;

        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.GetAsync($"/api/automoveis/{carro}")).Status);
        Assert.AreEqual(HttpStatusCode.NotFound, (await intrusa.DeleteAsync($"/api/automoveis/{carro}")).Status);
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/automoveis")).Registros.Count);
        Assert.AreEqual(1, (await dona.GetAsync("/api/automoveis")).Registros.Count, "a dona não perdeu o carro");
    }

    [TestMethod]
    public async Task Automovel_NaoPodeSerCadastradoNoGrupoDeOutraEmpresa()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var grupoDaDona = (await dona.CriarGrupoAsync()).Id;

        var resposta = await intrusa.CriarAutomovelAsync(grupoDaDona);

        Assert.IsFalse(resposta.Ok, "referenciar o grupo de outra empresa não pode funcionar: o carro ficaria ligado a dados que a empresa não enxerga");
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/automoveis")).Registros.Count);
    }

    [TestMethod]
    public async Task Plano_NaoPodeSerCriadoParaOGrupoDeOutraEmpresa()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var grupoDaDona = (await dona.CriarGrupoAsync()).Id;

        var resposta = await intrusa.CriarPlanoAsync(grupoDaDona);

        Assert.IsFalse(resposta.Ok, "um plano não pode ser pendurado no grupo de outra empresa");
        Assert.AreEqual(0, (await intrusa.GetAsync("/api/planos-cobranca")).Registros.Count);
    }

    [TestMethod]
    public async Task Automovel_NaoPodeSerMovidoParaOGrupoDeOutraEmpresaPelaEdicao()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var grupoDaDona = (await dona.CriarGrupoAsync()).Id;
        var grupoDaIntrusa = (await intrusa.CriarGrupoAsync()).Id;
        var carro = (await intrusa.CriarAutomovelAsync(grupoDaIntrusa, "ABC1D23")).Id;

        var edicao = await intrusa.PutAsync($"/api/automoveis/{carro}", new
        {
            placa = "ABC1D23", marca = "Fiat", cor = "Prata", modelo = "Uno", tipoCombustivel = "Gasolina", capacidadeTanque = 50m, ano = 2022,
            foto = (string?)null, grupoAutomovelId = grupoDaDona
        });

        Assert.IsFalse(edicao.Ok, "a edição não pode religar o carro ao grupo de outra empresa");
        Assert.AreEqual(grupoDaIntrusa, (await intrusa.GetAsync($"/api/automoveis/{carro}")).Propriedade("grupoAutomovelId").GetGuid());
    }

    [TestMethod]
    public async Task Plano_NaoPodeSerMovidoParaOGrupoDeOutraEmpresaPelaEdicao()
    {
        using var dona = await NovaEmpresa();
        using var intrusa = await NovaEmpresa();
        var grupoDaDona = (await dona.CriarGrupoAsync()).Id;
        var grupoDaIntrusa = (await intrusa.CriarGrupoAsync()).Id;
        var plano = (await intrusa.CriarPlanoAsync(grupoDaIntrusa)).Id;

        var edicao = await intrusa.PutAsync($"/api/planos-cobranca/{plano}",
            new { grupoAutomovelId = grupoDaDona, nome = "Plano de teste", precoDiaria = 100m, precoPorKm = 2m, kmLivreLimite = 100 });

        Assert.IsFalse(edicao.Ok);
        Assert.AreEqual(grupoDaIntrusa, (await intrusa.GetAsync($"/api/planos-cobranca/{plano}")).Propriedade("grupoAutomovelId").GetGuid());
    }

    [TestMethod]
    public async Task AutomoveisDisponiveis_NaoIncluemOCarroJaReservadoNoPeriodo_MasIncluemEmOutroPeriodo()
    {
        using var empresa = await NovaEmpresa();
        var cenario = await empresa.MontarCenarioAsync(carros: 2);
        var saida = DateTimeOffset.UtcNow.AddDays(5);
        await empresa.PostAsync("/api/alugueis", EmpresaDeTeste.CorpoDeAluguel(cenario, cenario.Automoveis[0], diasAteSaida: 5, duracao: 3));

        string Consulta(DateTimeOffset de, DateTimeOffset ate) =>
            $"/api/automoveis/disponiveis?dataSaida={Uri.EscapeDataString(de.ToString("o"))}&dataRetornoPrevisto={Uri.EscapeDataString(ate.ToString("o"))}";

        var noPeriodo = await empresa.GetAsync(Consulta(saida.AddDays(1), saida.AddDays(2)));
        var depois = await empresa.GetAsync(Consulta(saida.AddDays(10), saida.AddDays(12)));

        var idsNoPeriodo = noPeriodo.Registros.Select(r => r.GetProperty("id").GetGuid()).ToList();
        CollectionAssert.AreEqual(new[] { cenario.Automoveis[1] }, idsNoPeriodo, "só o carro livre aparece enquanto o outro está reservado");
        Assert.AreEqual(2, depois.Registros.Count, "fora do período reservado os dois estão livres");
    }

    [TestMethod]
    public async Task AutomoveisDisponiveis_ComRetornoAntesDaSaida_Recebe400()
    {
        using var empresa = await NovaEmpresa();
        var saida = DateTimeOffset.UtcNow.AddDays(5);

        var resposta = await empresa.GetAsync($"/api/automoveis/disponiveis?dataSaida={Uri.EscapeDataString(saida.ToString("o"))}&dataRetornoPrevisto={Uri.EscapeDataString(saida.AddDays(-1).ToString("o"))}");

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.Status);
        StringAssert.Contains(resposta.TextoDosErros, "após a data de saída");
    }

    // ---------------------------------------------------------------- papéis ------------------------------------------------------

    [TestMethod]
    public async Task Funcionario_ConsultaAFrota_MasNaoCadastraEditaNemExcluiCarros()
    {
        using var dona = await NovaEmpresa();
        using var funcionario = await EmpresaDeTeste.DoMesmoTenantAsync(_fabrica, dona, "Funcionario");
        var grupo = (await dona.CriarGrupoAsync()).Id;
        var carro = (await dona.CriarAutomovelAsync(grupo, "ABC1D23")).Id;

        Assert.AreEqual(HttpStatusCode.OK, (await funcionario.GetAsync("/api/automoveis")).Status);
        Assert.AreEqual(HttpStatusCode.OK, (await funcionario.GetAsync($"/api/automoveis/{carro}")).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.CriarAutomovelAsync(grupo)).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.DeleteAsync($"/api/automoveis/{carro}")).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.CriarGrupoAsync()).Status);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await funcionario.CriarPlanoAsync(grupo)).Status);
        Assert.AreEqual(1, (await dona.GetAsync("/api/automoveis")).Registros.Count, "nada foi alterado pelas tentativas do funcionário");
    }
}
