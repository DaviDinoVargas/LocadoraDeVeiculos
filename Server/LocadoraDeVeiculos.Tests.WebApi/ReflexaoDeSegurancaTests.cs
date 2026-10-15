using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

/// <summary>
/// Estudo com <c>System.Reflection</c>: em vez de listar à mão os endpoints que precisam de login, o teste LÊ os controllers
/// (<c>GetTypes</c>, <c>GetMethods</c>, <c>GetCustomAttributes</c>) e falha se qualquer ação nova ficar sem autorização declarada.
/// Os ataques por HTTP (<c>Testes-Seguranca/</c>) provam o comportamento; este teste prova que NENHUMA rota ficou de fora da regra.
/// </summary>
[TestClass]
[TestCategory("Reflexão")]
public sealed class ReflexaoDeSegurancaTests
{
    /// <summary>
    /// Rotas públicas POR DESENHO (sem login). Quem adiciona uma rota pública precisa escrevê-la aqui, de propósito, em revisão de código;
    /// uma rota nova sem [Authorize] e fora desta lista reprova o teste. `rotacionar` e `sair` não pedem login, mas exigem o cookie de
    /// sessão e o cabeçalho X-Locadora-Client.
    /// </summary>
    private static readonly string[] RotasPublicas =
    [
        "POST api/auth/registrar", "POST api/auth/autenticar", "POST api/auth/entrar-facial",
        "POST api/auth/esqueci-senha", "POST api/auth/redefinir-senha",
        "POST api/auth/rotacionar", "POST api/auth/sair"
    ];

    private static ApiFactory _fabrica = null!;
    private static IReadOnlyList<EndpointMapeado> _endpoints = null!;

    [ClassInitialize]
    public static void Preparar(TestContext _)
    {
        _fabrica = new ApiFactory();
        _endpoints = MapaDeEndpoints.PorReflexao(typeof(LocadoraDeVeiculos.WebApi.Program).Assembly);
    }

    [ClassCleanup]
    public static void Encerrar() => _fabrica.Dispose();

    // -------- Cenário 1: toda ação declara quem pode chamá-la --------

    [TestMethod]
    public void TodaAcaoDeController_TemAutorizacaoDeclarada_OuEstaNaListaDeRotasPublicas()
    {
        var semDecisao = _endpoints
            .Where(e => e.Acesso.AbertoAoPublico && !RotasPublicas.Contains(e.Chave))
            .Select(e => $"{e.Chave} ({e.Nome})")
            .ToList();

        Assert.AreEqual(0, semDecisao.Count,
            "Ação aberta ao público que não está na lista de rotas públicas (faltou [Authorize]?): " + string.Join("; ", semDecisao));
    }

    [TestMethod]
    public void RotasPublicas_SaoExatamenteAsDaLista_NemMaisNemMenos()
    {
        var reais = _endpoints.Where(e => e.Acesso.AbertoAoPublico).Select(e => e.Chave).Order().ToList();

        CollectionAssert.AreEqual(RotasPublicas.Order().ToList(), reais,
            "Rota pública nova (ou que deixou de ser pública) precisa ser decidida de propósito e refletida na lista deste teste.");
    }

    [TestMethod]
    public void RotasPublicas_TemLimiteDeTaxa_ParaBarrarForcaBruta()
    {
        var semLimite = _endpoints.Where(e => e.Acesso.AbertoAoPublico && string.IsNullOrEmpty(e.LimiteDeTaxa)).Select(e => e.Chave).ToList();

        Assert.AreEqual(0, semLimite.Count, "Rota pública sem [EnableRateLimiting]: " + string.Join("; ", semLimite));
    }

    [TestMethod]
    public void RotasQueUsamSoOCookie_ExigemOCabecalhoDoCliente()
    {
        // rotacionar/sair não pedem token (o cookie HttpOnly é a credencial), então o cabeçalho X-Locadora-Client é a defesa contra CSRF
        foreach (var chave in new[] { "POST api/auth/rotacionar", "POST api/auth/sair" })
            Assert.IsTrue(_endpoints.Single(e => e.Chave == chave).ExigeCabecalhoCliente, $"{chave} precisa de [ExigirCabecalhoCliente]");
    }

    [TestMethod]
    public void EscritaEmTaxasEServicos_ExigeOCargoEmpresa_MesmoComOControllerAbertoAoFuncionario()
    {
        // S32: o [Authorize] do método SOMA ao da classe; se alguém trocar por [Authorize(Roles = "Funcionario")] o papel efetivo vira vazio
        var taxas = _endpoints.Where(e => e.Controller == typeof(TaxaServicoController)).ToList();

        foreach (var escrita in taxas.Where(e => e.Verbo is "POST" or "PUT" or "DELETE"))
            CollectionAssert.AreEqual(new[] { "Empresa" }, escrita.Acesso.Papeis?.ToArray(), $"{escrita.Chave} deveria ser só da Empresa");

        foreach (var leitura in taxas.Where(e => e.Verbo == "GET"))
            CollectionAssert.AreEqual(new[] { "Empresa", "Funcionario" }, leitura.Acesso.Papeis?.ToArray(), $"{leitura.Chave} é consulta: Empresa ou Funcionario");
    }

    [TestMethod]
    public void ControllersDeGestao_SaoSoDaEmpresa()
    {
        foreach (var controller in new[] { typeof(FuncionarioController), typeof(AuditoriaController), typeof(GrupoAutomovelController) })
            foreach (var e in _endpoints.Where(e => e.Controller == controller))
                CollectionAssert.AreEqual(new[] { "Empresa" }, e.Acesso.Papeis?.ToArray(), $"{e.Chave} ({controller.Name}) deveria ser só da Empresa");
    }

    // -------- A análise realmente pega erro: controllers de propósito, uns certos e outros errados --------

    private sealed class SemNada : ControllerBase
    {
        [HttpGet("a")] public IActionResult Listar() => Ok();
    }

    [Authorize]
    private sealed class AutorizadoNaClasse : ControllerBase
    {
        [HttpGet("b")] public IActionResult Listar() => Ok();
    }

    private sealed class AutorizadoNoMetodo : ControllerBase
    {
        [Authorize(Roles = "Empresa")] [HttpPost("c")] public IActionResult Criar() => Ok();
    }

    [Authorize(Roles = "Empresa")]
    private sealed class AnonimoNoMetodo : ControllerBase
    {
        [AllowAnonymous] [HttpGet("d")] public IActionResult Publico() => Ok();
    }

    [Authorize(Roles = "Empresa,Funcionario")]
    private sealed class PapeisSomam : ControllerBase
    {
        [Authorize(Roles = "Empresa")] [HttpDelete("e/{id:guid}")] [HttpPut("e/{id:guid}")] public IActionResult Escrever(Guid id) => Ok();
    }

    [Authorize(Roles = "Empresa")]
    private sealed class PapeisIncompativeis : ControllerBase
    {
        [Authorize(Roles = "Funcionario")] [HttpGet("f")] public IActionResult Ninguem() => Ok();
    }

    private abstract class Abstrato : ControllerBase
    {
        [HttpGet("g")] public IActionResult Nada() => Ok();
    }

    [TestMethod]
    public void Analise_PegaUmaAcaoSemAutorizacao_EAceitaAsDemaisFormasDeProteger()
    {
        var mapa = MapaDeEndpoints.PorReflexao([typeof(SemNada), typeof(AutorizadoNaClasse), typeof(AutorizadoNoMetodo), typeof(AnonimoNoMetodo), typeof(Abstrato)])
            .ToDictionary(e => e.Chave);

        Assert.IsTrue(mapa["GET a"].Acesso.AbertoAoPublico, "o erro que o teste existe para pegar: nenhuma proteção declarada");
        Assert.AreEqual("Público (sem [Authorize])", mapa["GET a"].Acesso.Rotulo);
        Assert.AreEqual("Qualquer usuário autenticado", mapa["GET b"].Acesso.Rotulo, "[Authorize] na classe vale para a ação");
        Assert.AreEqual("Empresa", mapa["POST c"].Acesso.Rotulo, "[Authorize(Roles)] no método");
        Assert.AreEqual("Público ([AllowAnonymous])", mapa["GET d"].Acesso.Rotulo, "[AllowAnonymous] no método vence o [Authorize] da classe");
        Assert.IsFalse(mapa.ContainsKey("GET g"), "classe abstrata não é controller instanciável");
    }

    [TestMethod]
    public void Analise_SomaOsPapeisDoMetodoComOsDaClasse_EResolveRotaDeVariosVerbos()
    {
        var mapa = MapaDeEndpoints.PorReflexao([typeof(PapeisSomam), typeof(PapeisIncompativeis)]).ToDictionary(e => e.Chave);

        Assert.AreEqual("Empresa", mapa["DELETE e/{id}"].Acesso.Rotulo, "Empresa,Funcionario na classe ∩ Empresa no método = Empresa");
        Assert.IsTrue(mapa.ContainsKey("PUT e/{id}"), "uma ação com dois verbos gera duas linhas; ':guid' sai da rota");
        Assert.AreEqual(0, mapa["GET f"].Acesso.Papeis!.Count, "Empresa ∩ Funcionario = ninguém consegue (erro de configuração que a tabela deixa visível)");
    }

    // -------- Cenário 6: a tabela de rotas lida por reflexão bate com a API oficial do ASP.NET --------

    [TestMethod]
    public void TabelaPorReflexao_BateComAApiOficialDoAspNet_NasRotasENoAcesso()
    {
        var oficial = MapaDeEndpoints.PelaApiOficial(_fabrica.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>());

        var porReflexao = _endpoints.Select(e => $"{e.Verbo} {e.Rota} => {e.Acesso.Rotulo}").Order().ToList();
        var porApiOficial = oficial.Select(e => $"{e.Verbo} {e.Rota} => {e.Acesso.Rotulo}").Order().ToList();

        Assert.IsTrue(porReflexao.Count > 80, $"a API tem dezenas de endpoints; a leitura achou só {porReflexao.Count}");
        var soReflexao = porReflexao.Except(porApiOficial).ToList();
        var soOficial = porApiOficial.Except(porReflexao).ToList();
        Assert.AreEqual(0, soReflexao.Count + soOficial.Count,
            $"Só na reflexão: [{string.Join("; ", soReflexao)}] · Só na API oficial: [{string.Join("; ", soOficial)}]");
        CollectionAssert.AreEqual(porApiOficial, porReflexao);
    }

    [TestMethod]
    public void TabelaPorReflexao_CadaLinhaApontaParaOMesmoMetodoQueAApiOficialExecuta()
    {
        var oficial = MapaDeEndpoints.PelaApiOficial(_fabrica.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>())
            .ToDictionary(e => $"{e.Verbo} {e.Rota}", e => e.Acao);

        foreach (var e in _endpoints)
            Assert.AreEqual(oficial[e.Chave], e.Acao, $"{e.Chave}: a reflexão achou {e.Nome}, a API oficial executa outro método");
    }

    [TestMethod]
    public void TabelaPorReflexao_ENaoTemRotaDuplicada()
    {
        var repetidas = _endpoints.GroupBy(e => e.Chave).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.AreEqual(0, repetidas.Count, "duas ações para o mesmo verbo e rota: " + string.Join("; ", repetidas));
    }

    [TestMethod]
    public void TabelaPorReflexao_EGravadaForaDoRepositorio_EmMarkdownEJson()
    {
        var pasta = Environment.GetEnvironmentVariable("REFLEXAO_SAIDA") ?? Path.Combine(Path.GetTempPath(), "locadora-reflexao");
        Directory.CreateDirectory(pasta);

        File.WriteAllText(Path.Combine(pasta, "endpoints.md"), MapaDeEndpoints.EmMarkdown(_endpoints));
        File.WriteAllText(Path.Combine(pasta, "endpoints.json"), JsonSerializer.Serialize(
            _endpoints.Select(e => new { e.Verbo, rota = "/" + e.Rota, acao = e.Nome, acesso = e.Acesso.Rotulo, e.LimiteDeTaxa, e.ExigeCabecalhoCliente, e.Parametros, e.Retorno }),
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        var markdown = File.ReadAllLines(Path.Combine(pasta, "endpoints.md"));
        Assert.AreEqual(_endpoints.Count, markdown.Count(l => l.StartsWith("| ") && !l.StartsWith("| Verbo")));
        StringAssert.Contains(string.Join('\n', markdown), "| POST | `/api/auth/autenticar` |");
    }
}
