using LocadoraDeVeiculos.WebApi.Config;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using System.Net;

namespace LocadoraDeVeiculos.Tests.WebApi;

// Erros que o Kestrel detecta antes do controller (corpo acima do limite, requisição malformada) são do CLIENTE:
// 413/400, não 500. Um 500 aí mascara tentativas de abuso e polui o monitoramento.
[TestClass]
[TestCategory("Web API - Tratamento de erros")]
public sealed class TratamentoDeErrosTests
{
    private static async Task<HttpResponseMessage> Requisitar(Func<Exception> excecao)
    {
        using var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseGlobalExceptionHandler();
                    app.Run(_ => throw excecao());
                }))
            .Build();

        await host.StartAsync();

        return await host.GetTestClient().GetAsync("/");
    }

    private static async Task<string> PrimeiroErroAsync(HttpResponseMessage resposta)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("Erros")[0].GetString()!;
    }

    [TestMethod]
    public async Task CorpoAcimaDoLimite_Eh413_ComMensagemLimpa()
    {
        var resposta = await Requisitar(() => new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge));

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, resposta.StatusCode);
        var mensagem = await PrimeiroErroAsync(resposta);
        StringAssert.Contains(mensagem, "excede o tamanho permitido");
        Assert.IsFalse(mensagem.Contains("Kestrel"), "não pode vazar detalhes internos");
    }

    [TestMethod]
    public async Task RequisicaoMalformada_Eh400()
    {
        var resposta = await Requisitar(() => new BadHttpRequestException("Invalid request line.", StatusCodes.Status400BadRequest));

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        StringAssert.Contains(await PrimeiroErroAsync(resposta), "Requisição inválida");
    }

    [TestMethod]
    public async Task ExcecaoInesperada_Continua500_SemStackTrace()
    {
        var resposta = await Requisitar(() => new InvalidOperationException("segredo interno: senha=abc"));

        Assert.AreEqual(HttpStatusCode.InternalServerError, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.IsFalse(corpo.Contains("segredo interno"), "a mensagem da exceção não pode chegar ao cliente");
    }
}
