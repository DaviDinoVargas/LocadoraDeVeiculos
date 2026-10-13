using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAutenticacao;

// O serviço de ML exige o X-Internal-Token em TODO o módulo facial e não guarda nada (S1): as amostras
// ficam no armazenamento da Web API. Estes testes garantem que o cliente .NET envia o token em cada
// chamada, usa as rotas certas e só manda ao ML as amostras do próprio usuário.
[TestClass]
[TestCategory("Unidade - Autenticação Facial")]
public sealed class FacialAuthClientTests
{
    private const string Token = "segredo-de-teste-com-mais-de-trinta-e-dois-caracteres";

    private sealed class HandlerFalso(HttpStatusCode status, string corpo = "{}") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requisicoes { get; } = [];
        public List<string> Corpos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requisicoes.Add(request);
            Corpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FabricaFalsa(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://ml-api:8000")
        };
    }

    private sealed class ArmazenamentoEmMemoria : IArmazenamentoFacial
    {
        public Dictionary<string, List<IReadOnlyList<double>>> Dados { get; } = [];

        public Task<int> SalvarAsync(string pessoaId, IReadOnlyList<double> landmarks, CancellationToken cancellationToken)
        {
            if (!Dados.TryGetValue(pessoaId, out var lista))
                Dados[pessoaId] = lista = [];

            lista.Add(landmarks);
            return Task.FromResult(lista.Count);
        }

        public Task<IReadOnlyList<IReadOnlyList<double>>> ObterAsync(string pessoaId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IReadOnlyList<double>>>(Dados.TryGetValue(pessoaId, out var l) ? l : []);

        public Task<bool> ExisteAsync(string pessoaId, CancellationToken cancellationToken) =>
            Task.FromResult(Dados.TryGetValue(pessoaId, out var l) && l.Count > 0);

        public Task<bool> RemoverAsync(string pessoaId, CancellationToken cancellationToken) =>
            Task.FromResult(Dados.Remove(pessoaId));
    }

    private static FacialAuthClient CriarCliente(HandlerFalso handler, ArmazenamentoEmMemoria? armazenamento = null)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MlApi:InternalToken"] = Token })
            .Build();

        return new FacialAuthClient(new FabricaFalsa(handler), configuracao, armazenamento ?? new ArmazenamentoEmMemoria());
    }

    private static ArmazenamentoEmMemoria ComRostoDe(string pessoaId, params double[] landmarks)
    {
        var armazenamento = new ArmazenamentoEmMemoria();
        armazenamento.Dados[pessoaId] = [landmarks];
        return armazenamento;
    }

    private static void AssertTokenInterno(HttpRequestMessage requisicao)
    {
        Assert.IsTrue(requisicao.Headers.TryGetValues("X-Internal-Token", out var valores));
        Assert.AreEqual(Token, System.Linq.Enumerable.Single(valores));
    }

    [TestMethod]
    public async Task Verificar_EnviaTokenInternoEAsAmostrasDoProprioUsuario()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"match\":true,\"confidence\":0.95}");
        var armazenamento = ComRostoDe("usuario:a@b.com", 0.1, 0.2);
        armazenamento.Dados["usuario:outro@b.com"] = [[9.9, 9.9]];

        var resultado = await CriarCliente(handler, armazenamento).VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Bateu);
        Assert.AreEqual(HttpMethod.Post, handler.Requisicoes[0].Method);
        Assert.AreEqual("/face/compare", handler.Requisicoes[0].RequestUri!.AbsolutePath);
        AssertTokenInterno(handler.Requisicoes[0]);
        StringAssert.Contains(handler.Corpos[0], "0.1");
        Assert.IsFalse(handler.Corpos[0].Contains("9.9"), "amostras de outra pessoa não podem seguir para o ML");
        Assert.IsFalse(handler.Corpos[0].Contains("personId"), "o ML não precisa saber quem é a pessoa");
    }

    [TestMethod]
    public async Task Verificar_SemRostoCadastrado_NaoChamaOMl()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK);

        var resultado = await CriarCliente(handler).VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(resultado.Encontrado);
        Assert.AreEqual(0, handler.Requisicoes.Count);
    }

    [TestMethod]
    public async Task Verificar_422_SinalizaNenhumRostoNaImagem()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.UnprocessableEntity), ComRostoDe("usuario:a@b.com", 0.1))
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Encontrado);
        Assert.IsFalse(resultado.Bateu);
        Assert.IsTrue(resultado.NenhumRostoNaImagem);
    }

    [TestMethod]
    public async Task Verificar_400_ImagemInvalida_SinalizaNenhumRostoNaImagem()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.BadRequest), ComRostoDe("usuario:a@b.com", 0.1))
            .VerificarAsync("usuario:a@b.com", "lixo", CancellationToken.None);

        Assert.IsTrue(resultado.NenhumRostoNaImagem);
        Assert.IsFalse(resultado.Bateu);
    }

    [TestMethod]
    public async Task Verificar_ErroDoMl_NaoBate()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.Unauthorized), ComRostoDe("usuario:a@b.com", 0.1))
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(resultado.Bateu);
        Assert.IsFalse(resultado.NenhumRostoNaImagem);
    }

    [TestMethod]
    public async Task Cadastrar_PedeOsLandmarksAoMlComTokenInternoEGuardaNoArmazenamento()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"landmarks\":[0.5,0.25,0.75]}");
        var armazenamento = new ArmazenamentoEmMemoria();

        var resultado = await CriarCliente(handler, armazenamento).CadastrarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Sucesso);
        Assert.AreEqual(1, resultado.Amostras);
        Assert.AreEqual("/face/landmarks", handler.Requisicoes[0].RequestUri!.AbsolutePath);
        AssertTokenInterno(handler.Requisicoes[0]);
        CollectionAssert.AreEqual(new[] { 0.5, 0.25, 0.75 }, System.Linq.Enumerable.ToArray(armazenamento.Dados["usuario:a@b.com"][0]));
    }

    [TestMethod]
    public async Task Cadastrar_SemRostoNaImagem_NaoGuardaNada()
    {
        var armazenamento = new ArmazenamentoEmMemoria();

        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.UnprocessableEntity), armazenamento)
            .CadastrarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(resultado.Sucesso);
        Assert.AreEqual(0, armazenamento.Dados.Count);
    }

    [TestMethod]
    public async Task PossuiRosto_ConsultaOArmazenamentoSemChamarOMl()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK);
        var cliente = CriarCliente(handler, ComRostoDe("usuario:a@b.com", 0.1));

        Assert.IsTrue(await cliente.PossuiRostoAsync("usuario:a@b.com", CancellationToken.None));
        Assert.IsFalse(await cliente.PossuiRostoAsync("usuario:b@b.com", CancellationToken.None));
        Assert.AreEqual(0, handler.Requisicoes.Count);
    }

    [TestMethod]
    public async Task Remover_ApagaDoArmazenamentoSemChamarOMl()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK);
        var armazenamento = ComRostoDe("usuario:a@b.com", 0.1);

        var resultado = await CriarCliente(handler, armazenamento).RemoverAsync("usuario:a@b.com", CancellationToken.None);

        Assert.IsTrue(resultado);
        Assert.AreEqual(0, armazenamento.Dados.Count);
        Assert.AreEqual(0, handler.Requisicoes.Count);
    }

    [TestMethod]
    public async Task Remover_SemRostoCadastrado_ContaComoSucesso()
    {
        Assert.IsTrue(await CriarCliente(new HandlerFalso(HttpStatusCode.OK))
            .RemoverAsync("usuario:a@b.com", CancellationToken.None));
    }

    [TestMethod]
    public void PersonIdParaEmail_NormalizaMaiusculasEEspacos()
    {
        Assert.AreEqual("usuario:ana@loja.com", FacialAuthClient.PersonIdParaEmail("  Ana@Loja.COM "));
    }
}
