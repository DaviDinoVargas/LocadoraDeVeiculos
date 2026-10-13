using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S14: o que acontece com a sessão (cookie copiado, logout) também entra na trilha da empresa.
[TestClass]
[TestCategory("Web API - Auditoria da sessão")]
public sealed class AuditoriaDeSessaoTests
{
    private const string Senha = "Senha@12345";
    private const string NomeDoCookie = "LocadoraDeVeiculos.RefreshToken";

    private static ApiFactory NovaFabrica() => new(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000", ["REGISTRATION_LIMIT_PER_HOUR"] = "1000" });

    private static async Task<(string Email, string Token, string Cookie)> NovaEmpresaLogada(ApiFactory fabrica)
    {
        using var cliente = fabrica.CreateClient();
        var email = $"sessao-{Guid.NewGuid():N}@teste.local";
        var registro = await cliente.PostAsJsonAsync("/api/auth/registrar", new { nomeCompleto = "Empresa Teste", email, senha = Senha, confirmarSenha = Senha });
        Assert.AreEqual(HttpStatusCode.OK, registro.StatusCode);

        return await Entrar(fabrica, email);
    }

    private static async Task<(string Email, string Token, string Cookie)> Entrar(ApiFactory fabrica, string email)
    {
        using var cliente = fabrica.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        using var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync());

        return (email, json.RootElement.GetProperty("chave").GetString()!, CookieDe(login));
    }

    private static string CookieDe(HttpResponseMessage resposta)
    {
        var linha = resposta.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(NomeDoCookie + "=", StringComparison.Ordinal));
        return linha.Split(';')[0][(NomeDoCookie.Length + 1)..];
    }

    private static async Task<HttpResponseMessage> PostComCookie(ApiFactory fabrica, string rota, string cookie)
    {
        using var cliente = fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, rota);
        requisicao.Headers.Add("X-Locadora-Client", "web");
        requisicao.Headers.Add("Cookie", $"{NomeDoCookie}={cookie}");

        return await cliente.SendAsync(requisicao);
    }

    private static async Task<List<string>> Acoes(ApiFactory fabrica, string token)
    {
        using var cliente = fabrica.CriarClienteAutenticado(token);
        var resposta = await cliente.GetAsync("/api/auditoria?limite=200");
        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("registros").EnumerateArray().Select(r => r.GetProperty("acao").GetString()!).ToList();
    }

    [TestMethod]
    public async Task CookieJaTrocadoQueVolta_DerrubaAsSessoes_EFicaNaTrilhaDaEmpresa()
    {
        using var fabrica = NovaFabrica();
        var (email, _, cookieAntigo) = await NovaEmpresaLogada(fabrica);

        var primeira = await PostComCookie(fabrica, "/api/auth/rotacionar", cookieAntigo);
        var copia = await PostComCookie(fabrica, "/api/auth/rotacionar", cookieAntigo);

        Assert.AreEqual(HttpStatusCode.OK, primeira.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, copia.StatusCode, "o cookie velho não vale mais");
        var cookieNovo = CookieDe(primeira);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await PostComCookie(fabrica, "/api/auth/rotacionar", cookieNovo)).StatusCode,
            "e o reuso derrubou também a sessão nova (quem tem o novo e quem tem o velho podem ser pessoas diferentes)");

        var (_, tokenNovo, _) = await Entrar(fabrica, email);
        CollectionAssert.Contains(await Acoes(fabrica, tokenNovo), "ReusoDeRefreshToken");
    }

    [TestMethod]
    public async Task RotacaoNormal_NaoGeraEventoDeReuso()
    {
        using var fabrica = NovaFabrica();
        var (email, _, cookie) = await NovaEmpresaLogada(fabrica);

        Assert.AreEqual(HttpStatusCode.OK, (await PostComCookie(fabrica, "/api/auth/rotacionar", cookie)).StatusCode);

        var (_, token, _) = await Entrar(fabrica, email);
        CollectionAssert.DoesNotContain(await Acoes(fabrica, token), "ReusoDeRefreshToken");
    }

    [TestMethod]
    public async Task Logout_FicaNaTrilhaDaEmpresa()
    {
        using var fabrica = NovaFabrica();
        var (email, _, cookie) = await NovaEmpresaLogada(fabrica);

        var sair = await PostComCookie(fabrica, "/api/auth/sair", cookie);
        Assert.IsTrue(sair.IsSuccessStatusCode, sair.StatusCode.ToString());

        var (_, token, _) = await Entrar(fabrica, email);
        CollectionAssert.Contains(await Acoes(fabrica, token), "Logout");
    }
}
