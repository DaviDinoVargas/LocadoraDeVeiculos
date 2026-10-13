using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Config.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S4 / S8 / S9 / S10: proteções de borda da API, exercitadas contra o pipeline real.
[TestClass]
[TestCategory("Web API - Segurança HTTP")]
public sealed class SegurancaHttpTests
{
    [TestMethod]
    public async Task Resposta_TrazCabecalhosDeSeguranca()
    {
        using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/api/automoveis");

        Assert.AreEqual(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.AreEqual("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("DENY", resposta.Headers.GetValues("X-Frame-Options").Single());
        Assert.AreEqual("no-referrer", resposta.Headers.GetValues("Referrer-Policy").Single());
        StringAssert.Contains(resposta.Headers.GetValues("Permissions-Policy").Single(), "camera=()");
        StringAssert.Contains(resposta.Headers.GetValues("Content-Security-Policy").Single(), "frame-ancestors 'none'");
        Assert.AreEqual("no-store", resposta.Headers.CacheControl?.ToString());
    }

    [TestMethod]
    public async Task Autenticacao_PassandoDoLimite_RetornaTooManyRequests()
    {
        using var fabrica = new ApiFactory();
        using var cliente = fabrica.CreateClient();
        var corpo = new { email = "ninguem@teste.com", senha = "Errada@123" };

        HttpStatusCode? ultimo = null;
        for (var i = 0; i < SegurancaHttpConfig.LimiteAutenticacaoPorMinuto + 1; i++)
            ultimo = (await cliente.PostAsJsonAsync("/api/auth/autenticar", corpo)).StatusCode;

        Assert.AreEqual(HttpStatusCode.TooManyRequests, ultimo);
    }

    [TestMethod]
    public async Task LimiteGlobal_BloqueiaRequisicoesDemais_MesmoForaDaAutenticacao()
    {
        using var fabrica = new ApiFactory(new() { ["RATE_LIMIT_PER_MINUTE"] = "3" });
        using var cliente = fabrica.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await cliente.GetAsync("/api/automoveis")).StatusCode);

        CollectionAssert.AreEqual(
            new[]
            {
                HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests
            },
            statuses);
    }

    [TestMethod]
    public void LimiteDeCorpo_PadraoEhDeDoisMegabytes()
    {
        using var fabrica = new ApiFactory();

        var kestrel = fabrica.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.AreEqual(2L * 1024 * 1024, kestrel.Limits.MaxRequestBodySize);
    }

    [TestMethod]
    public void LimiteDeCorpo_PodeSerAjustadoPorConfiguracao()
    {
        using var fabrica = new ApiFactory(new() { ["MAX_REQUEST_BODY_BYTES"] = "4096" });

        var kestrel = fabrica.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.AreEqual(4096L, kestrel.Limits.MaxRequestBodySize);
    }

    [TestMethod]
    public void ProxiesConfiaveis_SemLista_NaoAcrescentaNenhumProxy()
    {
        var opcoes = new ForwardedHeadersOptions();
        var antes = opcoes.KnownProxies.ToArray();

        SegurancaHttpConfig.ConfigurarProxiesConfiaveis(opcoes, new ConfigurationBuilder().Build());

        Assert.AreEqual(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, opcoes.ForwardedHeaders);
        Assert.AreEqual(1, opcoes.ForwardLimit);
        CollectionAssert.AreEquivalent(antes, opcoes.KnownProxies.ToArray());
    }

    [TestMethod]
    public void ProxiesConfiaveis_AceitaIpECidr()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TRUSTED_PROXIES"] = "10.1.2.3; 172.18.0.0/16" })
            .Build();
        var opcoes = new ForwardedHeadersOptions();

        SegurancaHttpConfig.ConfigurarProxiesConfiaveis(opcoes, configuracao);

        Assert.IsTrue(opcoes.KnownProxies.Contains(IPAddress.Parse("10.1.2.3")));
        Assert.IsTrue(opcoes.KnownNetworks.Any(r => r.Prefix.Equals(IPAddress.Parse("172.18.0.0")) && r.PrefixLength == 16));
    }

    [TestMethod]
    [DataRow("nao-eh-ip")]
    [DataRow("10.0.0.0/abc")]
    public void ProxiesConfiaveis_ComValorInvalido_FalhaNaSubida(string valor)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TRUSTED_PROXIES"] = valor })
            .Build();

        Assert.ThrowsException<ArgumentException>(() =>
            SegurancaHttpConfig.ConfigurarProxiesConfiaveis(new ForwardedHeadersOptions(), configuracao));
    }
}
