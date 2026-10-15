using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;
using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LocadoraDeVeiculos.Tests.WebApi;

// A semente do autenticador e os códigos de recuperação valem como "segundo fator": quem lê uma cópia do banco e os encontra em texto puro
// gera os códigos de 6 dígitos sozinho. Aqui o que importa é o que ESTÁ GRAVADO e se o segundo fator continua funcionando por cima.
[TestClass]
[TestCategory("Web API - Segredos do segundo fator em repouso")]
public sealed class SegredosDoSegundoFatorHttpTests
{
    private const string Senha = "Senha@12345";

    private static ApiFactory Fabrica() => new(new() { ["RATE_LIMIT_PER_MINUTE"] = "5000", ["REGISTRATION_LIMIT_PER_HOUR"] = "5000" });

    private sealed record Conta(string Email, string Token, Guid Id, string Chave, IReadOnlyList<string> Codigos);

    private static async Task<Conta> ContaComDoisFatores(ApiFactory fabrica)
    {
        using var cliente = fabrica.CreateClient();
        var email = $"2fa-{Guid.NewGuid():N}@teste.local";
        var registro = await cliente.PostAsJsonAsync("/api/auth/registrar", new { nomeCompleto = "Empresa", email, senha = Senha, confirmarSenha = Senha });
        using var json = JsonDocument.Parse(await registro.Content.ReadAsStringAsync());
        var token = json.RootElement.GetProperty("chave").GetString()!;

        using var logado = fabrica.CriarClienteAutenticado(token);
        using var config = JsonDocument.Parse(await (await logado.PostAsync("/api/auth/2fa/iniciar", null)).Content.ReadAsStringAsync());
        var chave = config.RootElement.GetProperty("chave").GetString()!;
        var ativar = await logado.PostAsJsonAsync("/api/auth/2fa/ativar", new { codigo = Totp.Codigo(chave) });
        using var codigos = JsonDocument.Parse(await ativar.Content.ReadAsStringAsync());

        using var escopo = fabrica.Services.CreateScope();
        var gerenciador = escopo.ServiceProvider.GetRequiredService<UserManager<LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao.Usuario>>();
        var usuario = (await gerenciador.FindByEmailAsync(email))!;

        return new Conta(email, token, usuario.Id, chave, codigos.RootElement.GetProperty("codigos").EnumerateArray().Select(c => c.GetString()!).ToList());
    }

    /// <summary>O que o banco guarda de verdade (sem passar pelo Identity, que decifra).</summary>
    private static async Task<Dictionary<string, string>> GravadoNoBanco(ApiFactory fabrica, Guid usuarioId)
    {
        using var escopo = fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<LocadoraDeVeiculosDbContext>();

        return await contexto.UserTokens.AsNoTracking().Where(t => t.UserId == usuarioId).ToDictionaryAsync(t => t.Name, t => t.Value ?? "");
    }

    private static async Task GravarNoBanco(ApiFactory fabrica, Guid usuarioId, string nome, string valor)
    {
        using var escopo = fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<LocadoraDeVeiculosDbContext>();
        var linha = await contexto.UserTokens.SingleAsync(t => t.UserId == usuarioId && t.Name == nome);
        linha.Value = valor;
        await contexto.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> Entrar(ApiFactory fabrica, string email, string codigo)
    {
        using var cliente = fabrica.CreateClient();

        return await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha, codigoAutenticador = codigo });
    }

    [TestMethod]
    public async Task ChaveDoAutenticadorECodigosDeRecuperacao_NaoFicamEmTextoPuroNoBanco()
    {
        using var fabrica = Fabrica();
        var conta = await ContaComDoisFatores(fabrica);

        var gravado = await GravadoNoBanco(fabrica, conta.Id);

        var chaveGravada = gravado["AuthenticatorKey"];
        var codigosGravados = gravado["RecoveryCodes"];
        StringAssert.StartsWith(chaveGravada, UsuarioStoreComSegredosCifrados.Prefixo);
        StringAssert.StartsWith(codigosGravados, UsuarioStoreComSegredosCifrados.Prefixo);
        Assert.IsFalse(chaveGravada.Contains(conta.Chave.Replace(" ", "").ToUpperInvariant(), StringComparison.OrdinalIgnoreCase),
            "a semente que o usuário escaneou não pode estar legível no banco");
        foreach (var codigo in conta.Codigos)
            Assert.IsFalse(codigosGravados.Contains(codigo), "nenhum código de recuperação em texto puro");
    }

    [TestMethod]
    public async Task OSegundoFatorContinuaFuncionando_ComTotpECodigoDeRecuperacaoDeUsoUnico()
    {
        using var fabrica = Fabrica();
        var conta = await ContaComDoisFatores(fabrica);

        Assert.AreEqual(HttpStatusCode.OK, (await Entrar(fabrica, conta.Email, Totp.Codigo(conta.Chave))).StatusCode, "TOTP por cima da chave cifrada");

        Assert.AreEqual(HttpStatusCode.OK, (await Entrar(fabrica, conta.Email, conta.Codigos[0])).StatusCode, "código de recuperação por cima do valor cifrado");
        Assert.AreEqual(HttpStatusCode.BadRequest, (await Entrar(fabrica, conta.Email, conta.Codigos[0])).StatusCode, "o mesmo código não vale duas vezes");
        Assert.AreEqual(HttpStatusCode.OK, (await Entrar(fabrica, conta.Email, conta.Codigos[1])).StatusCode, "os outros códigos continuam valendo");

        using var logado = fabrica.CriarClienteAutenticado((await ContaLogada(fabrica, conta)).Token);
        using var status = JsonDocument.Parse(await (await logado.GetAsync("/api/auth/2fa")).Content.ReadAsStringAsync());
        Assert.AreEqual(7, status.RootElement.GetProperty("codigosDeRecuperacaoRestantes").GetInt32(), "três dos dez códigos foram gastos (dois no teste e um para abrir esta sessão)");
        StringAssert.StartsWith((await GravadoNoBanco(fabrica, conta.Id))["RecoveryCodes"], UsuarioStoreComSegredosCifrados.Prefixo,
            "depois de gastar um código a lista regravada continua cifrada");
    }

    private static async Task<Conta> ContaLogada(ApiFactory fabrica, Conta conta)
    {
        var resposta = await Entrar(fabrica, conta.Email, conta.Codigos[2]);
        using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

        return conta with { Token = json.RootElement.GetProperty("chave").GetString()! };
    }

    [TestMethod]
    public async Task ContaComChaveGravadaAntesDaCifra_EmTextoPuro_ContinuaEntrando()
    {
        using var fabrica = Fabrica();
        var conta = await ContaComDoisFatores(fabrica);
        await GravarNoBanco(fabrica, conta.Id, "AuthenticatorKey", conta.Chave.Replace(" ", "").ToUpperInvariant());   // como era gravado antes

        Assert.AreEqual(HttpStatusCode.OK, (await Entrar(fabrica, conta.Email, Totp.Codigo(conta.Chave))).StatusCode);
    }

    [TestMethod]
    public async Task ValorCifradoCopiadoParaOutraConta_NaoServe_ASegundaContaFicaSemSegundoFatorValido()
    {
        using var fabrica = Fabrica();
        var vitima = await ContaComDoisFatores(fabrica);
        var atacante = await ContaComDoisFatores(fabrica);
        var cifradoDaVitima = (await GravadoNoBanco(fabrica, vitima.Id))["AuthenticatorKey"];

        // quem escreve no banco copia a semente (cifrada) da vítima para a própria conta, esperando gerar os códigos da vítima
        await GravarNoBanco(fabrica, atacante.Id, "AuthenticatorKey", cifradoDaVitima);

        var comCodigoDaVitima = await Entrar(fabrica, atacante.Email, Totp.Codigo(vitima.Chave));
        var comCodigoDoAtacante = await Entrar(fabrica, atacante.Email, Totp.Codigo(atacante.Chave));
        Assert.AreEqual(HttpStatusCode.BadRequest, comCodigoDaVitima.StatusCode, "o valor está amarrado à conta de origem: não decifra na outra");
        Assert.AreEqual(HttpStatusCode.BadRequest, comCodigoDoAtacante.StatusCode, "e a conta fica sem chave utilizável (falha fechada, sem 500)");
    }

    [TestMethod]
    public async Task ValorCifradoAdulterado_FalhaFechado_SemErroInterno()
    {
        using var fabrica = Fabrica();
        var conta = await ContaComDoisFatores(fabrica);
        var original = (await GravadoNoBanco(fabrica, conta.Id))["AuthenticatorKey"];
        var adulterado = original[..^4] + (original.EndsWith("AAAA") ? "BBBB" : "AAAA");
        await GravarNoBanco(fabrica, conta.Id, "AuthenticatorKey", adulterado);

        var resposta = await Entrar(fabrica, conta.Email, Totp.Codigo(conta.Chave));

        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
