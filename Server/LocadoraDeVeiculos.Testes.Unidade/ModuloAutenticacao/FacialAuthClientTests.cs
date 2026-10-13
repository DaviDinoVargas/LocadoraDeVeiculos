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

        public Dictionary<string, string> Consentimentos { get; } = [];
        public List<string> Usos { get; } = [];

        public Task RegistrarConsentimentoAsync(string pessoaId, string versaoDoTexto, CancellationToken cancellationToken)
        {
            Consentimentos[pessoaId] = versaoDoTexto;
            return Task.CompletedTask;
        }

        public Task<string?> ObterVersaoDoConsentimentoAsync(string pessoaId, CancellationToken cancellationToken) =>
            Task.FromResult(Consentimentos.TryGetValue(pessoaId, out var v) ? v : null);

        public Task RegistrarUsoAsync(string pessoaId, CancellationToken cancellationToken)
        {
            Usos.Add(pessoaId);
            return Task.CompletedTask;
        }
    }

    private static FacialAuthClient CriarCliente(HandlerFalso handler, ArmazenamentoEmMemoria? armazenamento = null, bool exigirProvaDeVida = false)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MlApi:InternalToken"] = Token,
                ["ANTISPOOF_REQUIRED"] = exigirProvaDeVida ? "true" : "false"
            })
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
    public async Task Verificar_QuandoOMlConfirma_RegistraOUsoDoRosto_EQuandoNaoConfirma_NaoRegistra()
    {
        var armazenamento = ComRostoDe("usuario:a@b.com", 0.1, 0.2);

        await CriarCliente(new HandlerFalso(HttpStatusCode.OK, "{\"match\":false,\"confidence\":0.1}"), armazenamento)
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);
        Assert.AreEqual(0, armazenamento.Usos.Count, "rosto que não confere não renova a retenção");

        await CriarCliente(new HandlerFalso(HttpStatusCode.OK, "{\"match\":true,\"confidence\":0.95}"), armazenamento)
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "usuario:a@b.com" }, armazenamento.Usos, "uso bem-sucedido zera a contagem de 12 meses");
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
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"vetor\":[0.5,0.25,0.75]}");
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

    // ---------- prova de vida (S3) ----------

    private const string RespostaViva = "{\"match\":true,\"confidence\":0.95,\"provaDeVidaAtiva\":true,\"vivo\":true,\"pontuacaoDeVida\":0.97}";
    private const string RespostaFalsa = "{\"match\":true,\"confidence\":0.95,\"provaDeVidaAtiva\":true,\"vivo\":false,\"pontuacaoDeVida\":0.05}";
    private const string RespostaSemVida = "{\"match\":true,\"confidence\":0.95}";

    [TestMethod]
    public async Task Verificar_FotoQueNaoParecePessoaReal_NaoBate_MesmoComAGeometriaIgual_ENaoRenovaARetencao()
    {
        var armazenamento = ComRostoDe("usuario:a@b.com", 0.1, 0.2);

        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.OK, RespostaFalsa), armazenamento, exigirProvaDeVida: true)
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(resultado.Bateu);
        Assert.IsTrue(resultado.ProvaDeVidaReprovada);
        Assert.IsFalse(resultado.NenhumRostoNaImagem, "é tentativa de acesso, conta como falha (bloqueio de conta)");
        Assert.AreEqual(0, armazenamento.Usos.Count);
    }

    [TestMethod]
    public async Task Verificar_PessoaReal_Bate()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.OK, RespostaViva), ComRostoDe("usuario:a@b.com", 0.1), exigirProvaDeVida: true)
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Bateu);
        Assert.IsFalse(resultado.ProvaDeVidaReprovada);
    }

    [TestMethod]
    public async Task Verificar_ProvaDeVidaObrigatoria_MlQueNaoFezAChecagem_Recusa_FalhaFechada()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.OK, RespostaSemVida), ComRostoDe("usuario:a@b.com", 0.1), exigirProvaDeVida: true)
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(resultado.Bateu, "um ML antigo ou com a checagem desligada não pode liberar o acesso fora de desenvolvimento");
        Assert.IsTrue(resultado.ProvaDeVidaReprovada);
    }

    [TestMethod]
    public async Task Verificar_ProvaDeVidaNaoObrigatoria_MlSemChecagem_Bate()
    {
        var resultado = await CriarCliente(new HandlerFalso(HttpStatusCode.OK, RespostaSemVida), ComRostoDe("usuario:a@b.com", 0.1), exigirProvaDeVida: false)
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Bateu);
    }

    [TestMethod]
    public async Task Verificar_SemConfiguracaoExplicita_ForaDeDevelopmentExige_EmDevelopmentNao()
    {
        static FacialAuthClient Cliente(string? ambiente, HandlerFalso h, ArmazenamentoEmMemoria a) => new(
            new FabricaFalsa(h),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MlApi:InternalToken"] = Token,
                ["ASPNETCORE_ENVIRONMENT"] = ambiente
            }).Build(), a);

        var producao = await Cliente("Production", new HandlerFalso(HttpStatusCode.OK, RespostaSemVida), ComRostoDe("usuario:a@b.com", 0.1))
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);
        var desenvolvimento = await Cliente("Development", new HandlerFalso(HttpStatusCode.OK, RespostaSemVida), ComRostoDe("usuario:a@b.com", 0.1))
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);
        var semAmbiente = await Cliente(null, new HandlerFalso(HttpStatusCode.OK, RespostaSemVida), ComRostoDe("usuario:a@b.com", 0.1))
            .VerificarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(producao.Bateu);
        Assert.IsTrue(desenvolvimento.Bateu);
        Assert.IsFalse(semAmbiente.Bateu, "ambiente desconhecido conta como produção");
    }

    [TestMethod]
    public async Task Cadastrar_FotoQueNaoParecePessoaReal_NaoGuardaNada_EAvisaOMotivo()
    {
        var armazenamento = new ArmazenamentoEmMemoria();
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"vetor\":[0.5,0.25],\"provaDeVidaAtiva\":true,\"vivo\":false,\"pontuacaoDeVida\":0.1}");

        var resultado = await CriarCliente(handler, armazenamento, exigirProvaDeVida: true).CadastrarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsFalse(resultado.Sucesso);
        Assert.IsTrue(resultado.ProvaDeVidaReprovada);
        Assert.AreEqual(0, armazenamento.Dados.Count, "o rosto de uma foto não pode virar o cadastro");
    }

    [TestMethod]
    public async Task Cadastrar_PessoaReal_Guarda()
    {
        var armazenamento = new ArmazenamentoEmMemoria();
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"vetor\":[0.5,0.25],\"provaDeVidaAtiva\":true,\"vivo\":true,\"pontuacaoDeVida\":0.9}");

        var resultado = await CriarCliente(handler, armazenamento, exigirProvaDeVida: true).CadastrarAsync("usuario:a@b.com", "img", CancellationToken.None);

        Assert.IsTrue(resultado.Sucesso);
        Assert.AreEqual(1, armazenamento.Dados.Count);
    }

    [TestMethod]
    public void PersonIdParaEmail_NormalizaMaiusculasEEspacos()
    {
        Assert.AreEqual("usuario:ana@loja.com", FacialAuthClient.PersonIdParaEmail("  Ana@Loja.COM "));
    }
}
