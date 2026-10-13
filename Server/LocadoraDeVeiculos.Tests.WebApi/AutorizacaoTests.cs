using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;
using System.Net.Http.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// Autorização por papel contra o pipeline real: anônimo 401, papel insuficiente 403, papel certo passa.
// Espelha Client/src/app/core/permissoes.ts (o front só esconde o que o back já recusa).
[TestClass]
[TestCategory("Web API - Autorização")]
public sealed class AutorizacaoTests
{
    private static readonly string[] SomenteEmpresa =
    [
        "/api/auditoria", "/api/funcionarios", "/api/grupos-automovel", "/api/configuracoes",
        // O [Authorize] do método (Empresa,Funcionario) SOMA ao da classe (Empresa), então na prática é só Empresa.
        "/api/parceiros"
    ];

    private static readonly string[] EmpresaEFuncionario =
    [
        "/api/alugueis", "/api/alugueis/em-aberto", "/api/automoveis", "/api/automoveis/disponiveis",
        "/api/clientes", "/api/condutores", "/api/cupons", "/api/desafios-cupom", "/api/devolucoes",
        "/api/dashboard/resumo"
    ];

    private static IEnumerable<object[]> Rotas(IEnumerable<string> rotas) => rotas.Select(r => new object[] { r });

    public static IEnumerable<object[]> RotasSomenteEmpresa => Rotas(SomenteEmpresa);
    public static IEnumerable<object[]> RotasEmpresaEFuncionario => Rotas(EmpresaEFuncionario);
    public static IEnumerable<object[]> TodasAsRotas => Rotas(SomenteEmpresa.Concat(EmpresaEFuncionario));

    [TestMethod]
    [DynamicData(nameof(TodasAsRotas))]
    public async Task Anonimo_Recebe401(string rota)
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000" });
        using var cliente = fabrica.CreateClient();

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await cliente.GetAsync(rota)).StatusCode);
    }

    [TestMethod]
    [DynamicData(nameof(RotasSomenteEmpresa))]
    public async Task Funcionario_Recebe403_NasRotasSoDaEmpresa(string rota)
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000" });
        using var cliente = fabrica.CriarClienteAutenticado(await fabrica.CriarTokenAsync("Funcionario"));

        Assert.AreEqual(HttpStatusCode.Forbidden, (await cliente.GetAsync(rota)).StatusCode);
    }

    [TestMethod]
    [DynamicData(nameof(TodasAsRotas))]
    public async Task Empresa_AcessaTudo(string rota)
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000" });
        using var cliente = fabrica.CriarClienteAutenticado(await fabrica.CriarTokenAsync("Empresa"));

        var resposta = await cliente.GetAsync(rota);

        Assert.AreNotEqual(HttpStatusCode.Unauthorized, resposta.StatusCode, rota);
        Assert.AreNotEqual(HttpStatusCode.Forbidden, resposta.StatusCode, rota);
    }

    [TestMethod]
    [DynamicData(nameof(RotasEmpresaEFuncionario))]
    public async Task Funcionario_AcessaAsRotasCompartilhadas(string rota)
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000" });
        using var cliente = fabrica.CriarClienteAutenticado(await fabrica.CriarTokenAsync("Funcionario"));

        var resposta = await cliente.GetAsync(rota);

        Assert.AreNotEqual(HttpStatusCode.Unauthorized, resposta.StatusCode, rota);
        Assert.AreNotEqual(HttpStatusCode.Forbidden, resposta.StatusCode, rota);
    }

    [TestMethod]
    [DataRow("/api/automoveis")]
    [DataRow("/api/cupons")]
    [DataRow("/api/desafios-cupom")]
    [DataRow("/api/parceiros")]
    public async Task Funcionario_NaoCriaRecursosReservadosAEmpresa(string rota)
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000" });
        using var cliente = fabrica.CriarClienteAutenticado(await fabrica.CriarTokenAsync("Funcionario"));

        var resposta = await cliente.PostAsJsonAsync(rota, new { });

        Assert.AreEqual(HttpStatusCode.Forbidden, resposta.StatusCode, rota);
    }

    [TestMethod]
    public async Task TokenAdulteradoOuDeOutraChave_Recebe401()
    {
        using var fabrica = new ApiFactory();
        var token = await fabrica.CriarTokenAsync("Empresa");
        using var cliente = fabrica.CriarClienteAutenticado(token[..^3] + (token[^3] == 'A' ? "BBB" : "AAA"));

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/dashboard/resumo")).StatusCode);
    }

    [TestMethod]
    public async Task Auditoria_DaEmpresa_RetornaListaVaziaNoInicio()
    {
        using var fabrica = new ApiFactory();
        using var cliente = fabrica.CriarClienteAutenticado(await fabrica.CriarTokenAsync("Empresa"));

        var resposta = await cliente.GetAsync("/api/auditoria?limite=5");

        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        StringAssert.Contains(await resposta.Content.ReadAsStringAsync(), "registros");
    }
}
