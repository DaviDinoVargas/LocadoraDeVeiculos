using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAutenticacao;

// O serviço de ML exige o X-Internal-Token em TODO o módulo facial. Estes testes garantem que o
// cliente .NET o envia em cada operação e que usa o verbo/rota certos.
[TestClass]
[TestCategory("Unidade - Autenticação Facial")]
public sealed class FacialAuthClientTests
{
    private sealed class HandlerFalso(HttpStatusCode status, string corpo = "{}") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requisicoes { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requisicoes.Add(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FabricaFalsa(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://ml-api:8000")
        };
    }

    private static FacialAuthClient CriarCliente(HandlerFalso handler)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MlApi:InternalToken"] = "segredo-de-teste-com-mais-de-trinta-e-dois-caracteres" })
            .Build();

        return new FacialAuthClient(new FabricaFalsa(handler), configuracao);
    }

    private static void AssertTokenInterno(HttpRequestMessage requisicao)
    {
        Assert.IsTrue(requisicao.Headers.TryGetValues("X-Internal-Token", out var valores));
        Assert.AreEqual("segredo-de-teste-com-mais-de-trinta-e-dois-caracteres", System.Linq.Enumerable.Single(valores));
    }

    [TestMethod]
    public async Task Verificar_EnviaTokenInterno()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"match\":true,\"confidence\":0.95}");

        var resultado = await CriarCliente(handler).VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Bateu);
        Assert.AreEqual(HttpMethod.Post, handler.Requisicoes[0].Method);
        Assert.AreEqual("/face/verify", handler.Requisicoes[0].RequestUri!.AbsolutePath);
        AssertTokenInterno(handler.Requisicoes[0]);
    }

    [TestMethod]
    public async Task Verificar_422_SinalizaNenhumRostoNaImagem()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.UnprocessableEntity))
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Encontrado);
        Assert.IsFalse(resultado.Bateu);
        Assert.IsTrue(resultado.NenhumRostoNaImagem);
    }

    [TestMethod]
    public async Task Verificar_400_ImagemInvalida_SinalizaNenhumRostoNaImagem()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.BadRequest))
            .VerificarAsync("usuario:a@b.com", "lixo", CancellationToken.None);

        Assert.IsTrue(resultado.NenhumRostoNaImagem);
        Assert.IsFalse(resultado.Bateu);
    }

    [TestMethod]
    public async Task Verificar_404_SinalizaSemCadastro()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.NotFound))
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(resultado.Encontrado);
    }

    [TestMethod]
    public async Task Cadastrar_EnviaTokenInterno()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"ok\":true,\"personId\":\"x\",\"amostras\":2}");

        var resultado = await CriarCliente(handler).CadastrarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Sucesso);
        Assert.AreEqual(2, resultado.Amostras);
        AssertTokenInterno(handler.Requisicoes[0]);
    }

    [TestMethod]
    [DataRow("{\"personId\":\"usuario:a@b.com\",\"cadastrado\":true}", true)]
    [DataRow("{\"personId\":\"usuario:a@b.com\",\"cadastrado\":false}", false)]
    public async Task PossuiRosto_UsaGetComTokenInterno(string corpo, bool esperado)
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, corpo);

        var resultado = await CriarCliente(handler).PossuiRostoAsync("usuario:a@b.com", CancellationToken.None);

        Assert.AreEqual(esperado, resultado);
        Assert.AreEqual(HttpMethod.Get, handler.Requisicoes[0].Method);
        StringAssert.StartsWith(handler.Requisicoes[0].RequestUri!.AbsolutePath, "/face/status/");
        AssertTokenInterno(handler.Requisicoes[0]);
    }

    [TestMethod]
    public async Task Remover_UsaDeleteComTokenInterno()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK);

        var resultado = await CriarCliente(handler).RemoverAsync("usuario:a@b.com", CancellationToken.None);

        Assert.IsTrue(resultado);
        Assert.AreEqual(HttpMethod.Delete, handler.Requisicoes[0].Method);
        StringAssert.StartsWith(handler.Requisicoes[0].RequestUri!.AbsolutePath, "/face/");
        AssertTokenInterno(handler.Requisicoes[0]);
    }

    [TestMethod]
    public async Task Remover_404_ContaComoSucessoPoisJaNaoHaRosto()
    {
        Assert.IsTrue(await CriarCliente(new HandlerFalso(HttpStatusCode.NotFound))
            .RemoverAsync("usuario:a@b.com", CancellationToken.None));
    }

    [TestMethod]
    public async Task Remover_401DoMl_ContaComoFalha()
    {
        Assert.IsFalse(await CriarCliente(new HandlerFalso(HttpStatusCode.Unauthorized))
            .RemoverAsync("usuario:a@b.com", CancellationToken.None));
    }

    [TestMethod]
    public void PersonIdParaEmail_NormalizaMaiusculasEEspacos()
    {
        Assert.AreEqual("usuario:ana@loja.com", FacialAuthClient.PersonIdParaEmail("  Ana@Loja.COM "));
    }
}
