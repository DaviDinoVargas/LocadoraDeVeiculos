using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Config.Http;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// A renovação da sessão roda a cada carga de página: se dividisse o limite do login (10/min por IP), uma locadora inteira atrás do mesmo roteador
// seria deslogada. O cookie, por sua vez, só viaja para as rotas de autenticação.
[TestClass]
[TestCategory("Web API - Sessão")]
public sealed class SessaoHttpTests
{
    private const string Senha = "Senha@12345";
    private const string NomeDoCookie = "LocadoraDeVeiculos.RefreshToken";

    private static ApiFactory NovaFabrica() => new(new() { ["RATE_LIMIT_PER_MINUTE"] = "1000", ["REGISTRATION_LIMIT_PER_HOUR"] = "1000" });

    private static string CookieDe(HttpResponseMessage resposta) =>
        resposta.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(NomeDoCookie + "=", StringComparison.Ordinal));

    private static async Task<string> NovoCookie(ApiFactory fabrica)
    {
        using var cliente = fabrica.CreateClient();
        var email = $"sessao-{Guid.NewGuid():N}@teste.local";
        await cliente.PostAsJsonAsync("/api/auth/registrar", new { nomeCompleto = "Empresa Teste", email, senha = Senha, confirmarSenha = Senha });
        var login = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);

        return CookieDe(login);
    }

    private static async Task<HttpResponseMessage> Rotacionar(ApiFactory fabrica, string cookieCompleto)
    {
        using var cliente = fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/auth/rotacionar");
        requisicao.Headers.Add("X-Locadora-Client", "web");
        requisicao.Headers.Add("Cookie", cookieCompleto.Split(';')[0]);

        return await cliente.SendAsync(requisicao);
    }

    [TestMethod]
    public async Task RenovarASessaoVariasVezes_NaoConsomeOLimiteDoLogin()
    {
        using var fabrica = NovaFabrica();
        var cookie = await NovoCookie(fabrica);
        var status = new List<HttpStatusCode>();

        for (var i = 0; i < SegurancaHttpConfig.LimiteAutenticacaoPorMinuto * 2; i++)
        {
            var resposta = await Rotacionar(fabrica, cookie);
            status.Add(resposta.StatusCode);
            cookie = CookieDe(resposta);
        }

        Assert.IsTrue(status.All(s => s == HttpStatusCode.OK), $"20 recargas de página em seguida: {string.Join(",", status.Distinct())}");
    }

    [TestMethod]
    public async Task RenovarASessao_TemLimiteProprio_QueBarraAbusoEmMassa()
    {
        using var fabrica = NovaFabrica();
        var status = new List<HttpStatusCode>();

        // cookie inventado: cada tentativa dá 401, e passado do limite da rota o rate limit responde 429
        for (var i = 0; i < SegurancaHttpConfig.LimiteSessaoPorMinuto + 5; i++)
            status.Add((await Rotacionar(fabrica, $"{NomeDoCookie}=inventado{i}")).StatusCode);

        Assert.IsTrue(status.Take(SegurancaHttpConfig.LimiteSessaoPorMinuto).All(s => s == HttpStatusCode.Unauthorized));
        Assert.IsTrue(status.Skip(SegurancaHttpConfig.LimiteSessaoPorMinuto).All(s => s == HttpStatusCode.TooManyRequests),
            "depois de 60 tentativas no minuto o limite da sessão barra");
    }

    [TestMethod]
    public async Task OLimiteDoLogin_ContinuaEm10PorMinuto_MesmoComARenovacaoSolta()
    {
        using var fabrica = NovaFabrica();
        using var cliente = fabrica.CreateClient();
        var status = new List<HttpStatusCode>();

        for (var i = 0; i < SegurancaHttpConfig.LimiteAutenticacaoPorMinuto + 2; i++)
            status.Add((await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email = "ninguem@teste.local", senha = "Errada@123" })).StatusCode);

        Assert.AreEqual(HttpStatusCode.TooManyRequests, status.Last());
    }

    [TestMethod]
    public async Task OCookieDeSessao_SoViajaParaAsRotasDeAutenticacao_ComSecureHttpOnlyESameSite()
    {
        using var fabrica = NovaFabrica();

        var cookie = (await NovoCookie(fabrica)).ToLowerInvariant();

        StringAssert.Contains(cookie, "path=/api/auth");
        StringAssert.Contains(cookie, "secure");
        StringAssert.Contains(cookie, "httponly");
        StringAssert.Contains(cookie, "samesite=lax");
    }

    [TestMethod]
    public async Task OCookieApagadoNoLogout_UsaOMesmoPathDoCookieCriado()
    {
        // se o Path fosse outro, o navegador não apagaria o cookie e a sessão "encerrada" continuaria guardada
        using var fabrica = NovaFabrica();
        var cookie = await NovoCookie(fabrica);
        using var cliente = fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sair");
        requisicao.Headers.Add("X-Locadora-Client", "web");
        requisicao.Headers.Add("Cookie", cookie.Split(';')[0]);

        var resposta = await cliente.SendAsync(requisicao);

        var apagado = CookieDe(resposta).ToLowerInvariant();
        StringAssert.Contains(apagado, "path=/api/auth");
        StringAssert.Contains(apagado, "expires=");
    }
}
