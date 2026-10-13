using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;
using System.Net.Http.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S15 / S6: proteções específicas dos endpoints de autenticação.
[TestClass]
[TestCategory("Web API - Autenticação HTTP")]
public sealed class AutenticacaoHttpTests
{
    [TestMethod]
    [DataRow("/api/auth/rotacionar")]
    [DataRow("/api/auth/sair")]
    public async Task EndpointsQueAgemSoComCookie_ExigemOCabecalhoDoCliente(string rota)
    {
        using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        var semCabecalho = await cliente.PostAsync(rota, null);

        Assert.AreEqual(HttpStatusCode.Forbidden, semCabecalho.StatusCode);
    }

    [TestMethod]
    [DataRow("/api/auth/rotacionar")]
    [DataRow("/api/auth/sair")]
    public async Task ComOCabecalhoDoCliente_ChegaAoEndpoint_ESemCookieEh401(string rota)
    {
        using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, rota);
        requisicao.Headers.Add("X-Locadora-Client", "web");

        var resposta = await cliente.SendAsync(requisicao);

        Assert.AreEqual(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [TestMethod]
    public async Task CabecalhoComValorErrado_Eh403()
    {
        using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/auth/rotacionar");
        requisicao.Headers.Add("X-Locadora-Client", "outro");

        Assert.AreEqual(HttpStatusCode.Forbidden, (await cliente.SendAsync(requisicao)).StatusCode);
    }

    [TestMethod]
    public async Task Cadastro_ComLimiteAtingido_RetornaTooManyRequests()
    {
        using var fabrica = new ApiFactory(new() { ["REGISTRATION_LIMIT_PER_HOUR"] = "2" });
        using var cliente = fabrica.CreateClient();
        var corpo = new { nomeCompleto = "", email = "", senha = "", confirmarSenha = "" };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await cliente.PostAsJsonAsync("/api/auth/registrar", corpo)).StatusCode);

        Assert.IsFalse(statuses.Take(2).Any(s => s == HttpStatusCode.TooManyRequests), "as duas primeiras passam pelo limite");
        Assert.IsTrue(statuses.Skip(2).All(s => s == HttpStatusCode.TooManyRequests), "a partir da terceira o limite barra");
    }

    [TestMethod]
    public async Task Cadastro_TemLimiteProprioQueNaoConsomeODoLogin()
    {
        using var fabrica = new ApiFactory(new() { ["REGISTRATION_LIMIT_PER_HOUR"] = "1" });
        using var cliente = fabrica.CreateClient();
        var vazio = new { nomeCompleto = "", email = "", senha = "", confirmarSenha = "" };

        await cliente.PostAsJsonAsync("/api/auth/registrar", vazio);
        var segundoCadastro = await cliente.PostAsJsonAsync("/api/auth/registrar", vazio);
        var login = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email = "x@y.com", senha = "Errada@123" });

        Assert.AreEqual(HttpStatusCode.TooManyRequests, segundoCadastro.StatusCode);
        Assert.AreNotEqual(HttpStatusCode.TooManyRequests, login.StatusCode);
    }
}
