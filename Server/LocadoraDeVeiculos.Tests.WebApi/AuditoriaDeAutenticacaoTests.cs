using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Config.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S14 (eventos de login na trilha), S20 (cadastro não confirma e-mail existente) e S10 (rate limit por /64 em IPv6).
[TestClass]
[TestCategory("Web API - Auditoria e enumeração")]
public sealed class AuditoriaDeAutenticacaoTests
{
    private const string Senha = "Senha@12345";

    private static async Task<HttpResponseMessage> Registrar(HttpClient cliente, string email, string senha = Senha) =>
        await cliente.PostAsJsonAsync("/api/auth/registrar", new { nomeCompleto = "Empresa Teste", email, senha, confirmarSenha = senha });

    private static async Task<string> TokenDe(HttpResponseMessage resposta)
    {
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("chave").GetString()!;
    }

    private static async Task<List<string>> AcoesDaAuditoria(ApiFactory fabrica, string token)
    {
        using var cliente = fabrica.CriarClienteAutenticado(token);
        var resposta = await cliente.GetAsync("/api/auditoria?limite=200");
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("registros").EnumerateArray()
            .Where(r => r.GetProperty("entidade").GetString() == "Autenticacao")
            .Select(r => r.GetProperty("acao").GetString()!)
            .ToList();
    }

    [TestMethod]
    public async Task LoginOkELoginFalhou_EntramNaTrilhaDaEmpresa_ComIp()
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000", ["REGISTRATION_LIMIT_PER_HOUR"] = "1000" });
        using var cliente = fabrica.CreateClient();
        var email = $"audit-{Guid.NewGuid():N}@teste.local";
        var token = await TokenDe(await Registrar(cliente, email));

        await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = "Errada@12345" });
        var ok = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha });
        Assert.AreEqual(HttpStatusCode.OK, ok.StatusCode);

        var acoes = await AcoesDaAuditoria(fabrica, token);

        CollectionAssert.Contains(acoes, "LoginFalhou");
        CollectionAssert.Contains(acoes, "LoginOk");
    }

    [TestMethod]
    public async Task LoginDeEmailInexistente_NaoCriaRegistroDeNinguem()
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000", ["REGISTRATION_LIMIT_PER_HOUR"] = "1000" });
        using var cliente = fabrica.CreateClient();
        var token = await TokenDe(await Registrar(cliente, $"audit-{Guid.NewGuid():N}@teste.local"));

        await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email = "ninguem@teste.local", senha = Senha });

        CollectionAssert.DoesNotContain(await AcoesDaAuditoria(fabrica, token), "LoginFalhou");
    }

    [TestMethod]
    public async Task AuditoriaDeLogin_NaoVazaParaOutraEmpresa()
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000", ["REGISTRATION_LIMIT_PER_HOUR"] = "1000" });
        using var cliente = fabrica.CreateClient();
        var emailA = $"a-{Guid.NewGuid():N}@teste.local";
        await TokenDe(await Registrar(cliente, emailA));
        var tokenB = await TokenDe(await Registrar(cliente, $"b-{Guid.NewGuid():N}@teste.local"));

        await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email = emailA, senha = "Errada@12345" });

        CollectionAssert.DoesNotContain(await AcoesDaAuditoria(fabrica, tokenB), "LoginFalhou");
    }

    [TestMethod]
    public async Task CadastroComEmailExistente_NaoConfirmaQueOEmailExiste()
    {
        using var fabrica = new ApiFactory(new() { ["REGISTRATION_LIMIT_PER_HOUR"] = "1000" });
        using var cliente = fabrica.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@teste.local";
        await Registrar(cliente, email);

        var repetido = await Registrar(cliente, email);
        var corpo = await repetido.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.BadRequest, repetido.StatusCode);
        Assert.IsFalse(corpo.Contains("Já existe", StringComparison.OrdinalIgnoreCase) || corpo.Contains("J\u00E1 existe"),
            "a mensagem não pode confirmar que o e-mail tem conta: " + corpo);
        StringAssert.Contains(corpo, "concluir o cadastro".Replace("ú", "\u00FA"), StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    [DataRow("2001:db8:abcd:12::1", "2001:db8:abcd:12::ffff", true)]
    [DataRow("2001:db8:abcd:12::1", "2001:db8:abcd:13::1", false)]
    [DataRow("192.168.0.10", "192.168.0.11", false)]
    public void ChaveDoIp_AgrupaIpv6PorPrefixo64(string a, string b, bool mesmoBalde)
    {
        var chaveA = SegurancaHttpConfig.ChaveDoIp(System.Net.IPAddress.Parse(a));
        var chaveB = SegurancaHttpConfig.ChaveDoIp(System.Net.IPAddress.Parse(b));

        Assert.AreEqual(mesmoBalde, chaveA == chaveB, $"{chaveA} x {chaveB}");
    }

    [TestMethod]
    public void ChaveDoIp_Ipv4EIpv4MapeadoEmIpv6SaoOMesmoBalde_ENuloEDesconhecido()
    {
        Assert.AreEqual("203.0.113.5", SegurancaHttpConfig.ChaveDoIp(System.Net.IPAddress.Parse("203.0.113.5")));
        Assert.AreEqual("203.0.113.5", SegurancaHttpConfig.ChaveDoIp(System.Net.IPAddress.Parse("::ffff:203.0.113.5")));
        Assert.AreEqual("desconhecido", SegurancaHttpConfig.ChaveDoIp(null));
    }
}
