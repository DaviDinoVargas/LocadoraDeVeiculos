using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S10 / S28: quem erra a senha de um IP só trava aquele IP para aquela conta, não o dono; 10 erros no total travam a conta.
[TestClass]
[TestCategory("Web API - Bloqueio por conta e IP")]
public sealed class BloqueioPorContaEIpTests
{
    private const string Email = "dono@loja.com";
    private const string Senha = "Senha@12345";
    private const string Errada = "Errada@123456";

    private static async Task Errar(ApiFactory fabrica, string ip, int vezes, string email = Email)
    {
        for (var i = 0; i < vezes; i++)
            Assert.AreEqual(HttpStatusCode.BadRequest, (await fabrica.LoginDoIpAsync(ip, email, Errada)).StatusCode);
    }

    private static async Task<(int Falhas, bool Bloqueada)> SituacaoDaConta(ApiFactory fabrica)
    {
        using var escopo = fabrica.Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var conta = (await usuarios.FindByEmailAsync(Email))!;
        return (await usuarios.GetAccessFailedCountAsync(conta), await usuarios.IsLockedOutAsync(conta));
    }

    [TestMethod]
    public async Task UmAtacanteSozinho_EsgotaOProprioLimite_ENaoTrancaODonoParaFora()
    {
        using var fabrica = new ApiFactory();
        await fabrica.CriarContaAsync(Email, Senha);

        await Errar(fabrica, "203.0.113.10", 5);

        var atacanteComSenhaCerta = await fabrica.LoginDoIpAsync("203.0.113.10", Email, Senha);
        var dono = await fabrica.LoginDoIpAsync("198.51.100.7", Email, Senha);

        Assert.AreEqual(HttpStatusCode.BadRequest, atacanteComSenhaCerta.StatusCode, "o IP que errou 5 vezes é barrado até com a senha certa");
        Assert.AreEqual(HttpStatusCode.OK, dono.StatusCode, "o dono, de outro IP, entra normalmente");
    }

    [TestMethod]
    public async Task OsErrosDoIpBarrado_NaoSomamAoBloqueioDaConta()
    {
        using var fabrica = new ApiFactory();
        await fabrica.CriarContaAsync(Email, Senha);

        await Errar(fabrica, "203.0.113.10", 5);
        await Errar(fabrica, "203.0.113.10", 4);   // continua atacando (abaixo de 10/min, do limite por IP): já está barrado, nada disso chega à conta

        var (falhas, bloqueada) = await SituacaoDaConta(fabrica);
        Assert.AreEqual(5, falhas);
        Assert.IsFalse(bloqueada);
    }

    [TestMethod]
    public async Task ErrosEmVariosIps_ChegamAoLimiteDaConta_ETravamQualquerUm()
    {
        using var fabrica = new ApiFactory();
        await fabrica.CriarContaAsync(Email, Senha);

        await Errar(fabrica, "203.0.113.10", 5);
        await Errar(fabrica, "203.0.113.11", 5);

        var (_, bloqueada) = await SituacaoDaConta(fabrica);
        Assert.IsTrue(bloqueada, "10 erros no total (força bruta distribuída) travam a conta");
        Assert.AreEqual(HttpStatusCode.BadRequest, (await fabrica.LoginDoIpAsync("198.51.100.7", Email, Senha)).StatusCode);
    }

    [TestMethod]
    public async Task EmailInexistente_TambemEhLimitadoPorIp_ComAMesmaResposta()
    {
        using var fabrica = new ApiFactory();

        await Errar(fabrica, "203.0.113.10", 8, "ninguem@loja.com");

        var resposta = await fabrica.LoginDoIpAsync("203.0.113.10", "ninguem@loja.com", Errada);
        Assert.AreEqual(HttpStatusCode.BadRequest, resposta.StatusCode);
        StringAssert.Contains(await resposta.Content.ReadAsStringAsync(), "Usuário ou senha inválidos.");
    }

    [TestMethod]
    public async Task LoginCerto_ZeraOContadorDoPar()
    {
        using var fabrica = new ApiFactory();
        await fabrica.CriarContaAsync(Email, Senha);

        await Errar(fabrica, "203.0.113.10", 4);
        Assert.AreEqual(HttpStatusCode.OK, (await fabrica.LoginDoIpAsync("203.0.113.10", Email, Senha)).StatusCode);
        await Errar(fabrica, "203.0.113.10", 4);

        Assert.AreEqual(HttpStatusCode.OK, (await fabrica.LoginDoIpAsync("203.0.113.10", Email, Senha)).StatusCode,
            "4 erros, acerto, 4 erros: o acerto zerou a contagem, então ainda não passou de 5");
    }
}
