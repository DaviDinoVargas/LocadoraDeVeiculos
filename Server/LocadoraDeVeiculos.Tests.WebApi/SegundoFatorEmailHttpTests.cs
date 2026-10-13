using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S18 pelo pipeline real: 2FA no login (428 sem o código) e "esqueci minha senha" sem enumeração.
[TestClass]
[TestCategory("Web API - Segundo fator e recuperação")]
public sealed class SegundoFatorEmailHttpTests
{
    private const string Senha = "Senha@12345";

    private static ApiFactory Fabrica() => new(new() { ["RATE_LIMIT_PER_MINUTE"] = "5000", ["REGISTRATION_LIMIT_PER_HOUR"] = "5000" });

    private static async Task<(string Email, string Token)> Registrar(HttpClient cliente)
    {
        var email = $"fa-{Guid.NewGuid():N}@teste.local";
        var resposta = await cliente.PostAsJsonAsync("/api/auth/registrar", new { nomeCompleto = "Empresa", email, senha = Senha, confirmarSenha = Senha });
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return (email, json.RootElement.GetProperty("chave").GetString()!);
    }

    private static async Task<string> AtivarDoisFatores(ApiFactory fabrica, string token)
    {
        using var logado = fabrica.CriarClienteAutenticado(token);
        var iniciar = await logado.PostAsync("/api/auth/2fa/iniciar", null);
        using var config = JsonDocument.Parse(await iniciar.Content.ReadAsStringAsync());
        var chave = config.RootElement.GetProperty("chave").GetString()!;

        var ativar = await logado.PostAsJsonAsync("/api/auth/2fa/ativar", new { codigo = Totp.Codigo(chave) });
        Assert.AreEqual(HttpStatusCode.OK, ativar.StatusCode, await ativar.Content.ReadAsStringAsync());

        return chave;
    }

    [TestMethod]
    public async Task Endpoints2fa_ExigemLogin()
    {
        using var fabrica = Fabrica();
        using var anonimo = fabrica.CreateClient();

        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/auth/2fa")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/auth/2fa/iniciar", null)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/auth/2fa/ativar", new { codigo = "123456" })).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/auth/2fa/desativar", new { senha = "x", codigo = "1" })).StatusCode);
    }

    [TestMethod]
    public async Task ComSegundoFatorAtivo_LoginSemCodigoDa428_ComCodigoErradoDa400_ComCodigoCertoEntra()
    {
        using var fabrica = Fabrica();
        using var cliente = fabrica.CreateClient();
        var (email, token) = await Registrar(cliente);
        var chave = await AtivarDoisFatores(fabrica, token);

        var semCodigo = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha });
        Assert.AreEqual((HttpStatusCode)428, semCodigo.StatusCode);

        var senhaErradaSemCodigo = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = "Errada@12345" });
        Assert.AreEqual(HttpStatusCode.BadRequest, senhaErradaSemCodigo.StatusCode, "senha errada nunca chega a pedir o código (não revela que há 2FA)");

        var errado = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha, codigoAutenticador = "000000" });
        Assert.IsTrue(errado.StatusCode == HttpStatusCode.BadRequest || Totp.Codigo(chave) == "000000");

        var certo = await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha, codigoAutenticador = Totp.Codigo(chave) });
        Assert.AreEqual(HttpStatusCode.OK, certo.StatusCode);
    }

    [TestMethod]
    public async Task Status2fa_RefleteAAtivacao_ECodigosDeRecuperacaoVoltamUmaVez()
    {
        using var fabrica = Fabrica();
        using var cliente = fabrica.CreateClient();
        var (_, token) = await Registrar(cliente);
        using var logado = fabrica.CriarClienteAutenticado(token);

        using var antes = JsonDocument.Parse(await (await logado.GetAsync("/api/auth/2fa")).Content.ReadAsStringAsync());
        Assert.IsFalse(antes.RootElement.GetProperty("habilitado").GetBoolean());

        var iniciar = await logado.PostAsync("/api/auth/2fa/iniciar", null);
        using var config = JsonDocument.Parse(await iniciar.Content.ReadAsStringAsync());
        StringAssert.StartsWith(config.RootElement.GetProperty("uri").GetString(), "otpauth://totp/");
        var ativar = await logado.PostAsJsonAsync("/api/auth/2fa/ativar", new { codigo = Totp.Codigo(config.RootElement.GetProperty("chave").GetString()!) });
        using var codigos = JsonDocument.Parse(await ativar.Content.ReadAsStringAsync());
        Assert.AreEqual(10, codigos.RootElement.GetProperty("codigos").GetArrayLength());

        using var depois = JsonDocument.Parse(await (await logado.GetAsync("/api/auth/2fa")).Content.ReadAsStringAsync());
        Assert.IsTrue(depois.RootElement.GetProperty("habilitado").GetBoolean());
        Assert.AreEqual(10, depois.RootElement.GetProperty("codigosDeRecuperacaoRestantes").GetInt32());
    }

    [TestMethod]
    public async Task EsqueciSenha_ResponderIgualParaEmailExistenteEInexistente()
    {
        using var fabrica = Fabrica();
        using var cliente = fabrica.CreateClient();
        var (email, _) = await Registrar(cliente);

        var existente = await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new { email });
        var inexistente = await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new { email = "ninguem@teste.local" });
        var lixo = await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new { email = "lixo" });

        Assert.AreEqual(HttpStatusCode.Accepted, existente.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, inexistente.StatusCode);
        Assert.AreEqual(HttpStatusCode.Accepted, lixo.StatusCode);
        Assert.AreEqual(await existente.Content.ReadAsStringAsync(), await inexistente.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task RedefinirSenha_ComTokenInvalido_Eh400_ComMensagemUnica()
    {
        using var fabrica = Fabrica();
        using var cliente = fabrica.CreateClient();
        var (email, _) = await Registrar(cliente);

        var tokenRuim = await cliente.PostAsJsonAsync("/api/auth/redefinir-senha", new { email, token = "abc", novaSenha = "NovaSenha@456", confirmarNovaSenha = "NovaSenha@456" });
        var semConta = await cliente.PostAsJsonAsync("/api/auth/redefinir-senha", new { email = "ninguem@teste.local", token = "abc", novaSenha = "NovaSenha@456", confirmarNovaSenha = "NovaSenha@456" });

        Assert.AreEqual(HttpStatusCode.BadRequest, tokenRuim.StatusCode);
        Assert.AreEqual(await tokenRuim.Content.ReadAsStringAsync(), await semConta.Content.ReadAsStringAsync(), "não revela se o e-mail tem conta");
    }

    [TestMethod]
    public async Task EsqueciSenha_TemOLimiteDeAutenticacaoPorIp()
    {
        using var fabrica = new ApiFactory(new() { ["REGISTRATION_LIMIT_PER_HOUR"] = "5000" });
        using var cliente = fabrica.CreateClient();

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
            codigos.Add((await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new { email = $"x{i}@teste.local" })).StatusCode);

        Assert.IsTrue(codigos.Contains(HttpStatusCode.TooManyRequests), "10/min por IP vale também aqui");
    }
}
