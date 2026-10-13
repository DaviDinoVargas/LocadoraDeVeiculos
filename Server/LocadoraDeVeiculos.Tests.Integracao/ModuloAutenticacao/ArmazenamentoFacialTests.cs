using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// S1: os landmarks faciais ficam no banco, cifrados, e só quem tem a chave os lê.
[TestClass]
[TestCategory("Integração - Armazenamento facial")]
public sealed class ArmazenamentoFacialTests
{
    private static byte[] Chave(byte valor) => Enumerable.Repeat(valor, 32).ToArray();

    private static CifradorFacial NovoCifrador(params (byte Id, byte Valor)[] chaves) =>
        new(chaves.ToDictionary(c => c.Id, c => Chave(c.Valor)));

    private static LocadoraDeVeiculosDbContext NovoContexto(string banco) =>
        new(new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>().UseInMemoryDatabase(banco).Options);

    private static string NovoBanco() => Guid.NewGuid().ToString();

    // ---------- cifrador ----------

    [TestMethod]
    public void Cifrador_IdaEVolta()
    {
        var cifrador = NovoCifrador((1, 7));
        var texto = new byte[] { 1, 2, 3, 4, 5 };

        var cifrado = cifrador.Cifrar(texto, "usuario:a@b.com");

        CollectionAssert.AreEqual(texto, cifrador.Decifrar(cifrado, "usuario:a@b.com"));
        CollectionAssert.AreNotEqual(texto, cifrado.Skip(29).ToArray(), "o texto não pode aparecer em claro");
    }

    [TestMethod]
    public void Cifrador_MesmoTextoGeraConteudosDiferentes()
    {
        var cifrador = NovoCifrador((1, 7));
        var texto = new byte[] { 1, 2, 3 };

        CollectionAssert.AreNotEqual(cifrador.Cifrar(texto, "x"), cifrador.Cifrar(texto, "x"));
    }

    [TestMethod]
    public void Cifrador_LinhaCopiadaParaOutraPessoa_NaoDecifra()
    {
        var cifrador = NovoCifrador((1, 7));
        var cifrado = cifrador.Cifrar([1, 2, 3], "usuario:a@b.com");

        Assert.ThrowsException<AuthenticationTagMismatchException>(() => cifrador.Decifrar(cifrado, "usuario:outro@b.com"));
    }

    [TestMethod]
    public void Cifrador_ConteudoAdulterado_NaoDecifra()
    {
        var cifrador = NovoCifrador((1, 7));
        var cifrado = cifrador.Cifrar([1, 2, 3], "x");
        cifrado[^1] ^= 0xFF;

        Assert.ThrowsException<AuthenticationTagMismatchException>(() => cifrador.Decifrar(cifrado, "x"));
    }

    [TestMethod]
    public void Cifrador_RotacaoDeChave_ContinuaLendoOAntigoEUsaAMaisNovaNoNovo()
    {
        var antigo = NovoCifrador((1, 7)).Cifrar([9, 9], "x");
        var rotacionado = NovoCifrador((1, 7), (2, 8));

        CollectionAssert.AreEqual(new byte[] { 9, 9 }, rotacionado.Decifrar(antigo, "x"));
        Assert.AreEqual(2, rotacionado.Cifrar([1], "x")[0], "novas amostras usam a chave de maior id");
    }

    [TestMethod]
    public void Cifrador_ChaveRemovida_FalhaEmVezDeDevolverLixo()
    {
        var antigo = NovoCifrador((1, 7)).Cifrar([9, 9], "x");

        Assert.ThrowsException<CryptographicException>(() => NovoCifrador((2, 8)).Decifrar(antigo, "x"));
    }

    [TestMethod]
    public void Cifrador_ChaveComTamanhoErrado_Recusa()
    {
        Assert.ThrowsException<ArgumentException>(() =>
            new CifradorFacial(new Dictionary<byte, byte[]> { [1] = new byte[16] }));
    }

    // ---------- configuração ----------

    private static IConfiguration Config(string? ambiente, string? chaves)
    {
        var valores = new Dictionary<string, string?>();

        if (ambiente is not null) valores["ASPNETCORE_ENVIRONMENT"] = ambiente;
        if (chaves is not null) valores[CifradorFacial.ChaveDeConfiguracao] = chaves;

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [TestMethod]
    public void Configuracao_EmDesenvolvimentoSemChave_UsaChaveDeDesenvolvimento()
    {
        var cifrador = CifradorFacial.DaConfiguracao(Config("Development", null));

        CollectionAssert.AreEqual(new byte[] { 1 }, cifrador.Decifrar(cifrador.Cifrar([1], "x"), "x"));
    }

    [TestMethod]
    [DataRow("Production", null)]
    [DataRow(null, null)]
    [DataRow("Production", "1:nao-eh-base64!")]
    [DataRow("Production", "abc")]
    [DataRow("Production", "0:AAAA")]
    public void Configuracao_ForaDeDesenvolvimentoOuInvalida_Recusa(string? ambiente, string? chaves)
    {
        Assert.ThrowsException<InvalidOperationException>(() => CifradorFacial.DaConfiguracao(Config(ambiente, chaves)));
    }

    [TestMethod]
    public void Configuracao_LeVariasChavesEUsaAMaiorId()
    {
        var chaveAntiga = Convert.ToBase64String(Chave(1));
        var chaveNova = Convert.ToBase64String(Chave(2));

        var cifrador = CifradorFacial.DaConfiguracao(Config("Production", $"1:{chaveAntiga}; 2:{chaveNova}"));

        Assert.AreEqual(2, cifrador.Cifrar([1], "x")[0]);
    }

    // ---------- armazenamento ----------

    [TestMethod]
    public async Task Armazenamento_SalvaCifradoEDevolveOsMesmosLandmarks()
    {
        var banco = NovoBanco();
        await using var contexto = NovoContexto(banco);
        var armazenamento = new ArmazenamentoFacialEmOrm(contexto, NovoCifrador((1, 7)));

        await armazenamento.SalvarAsync("usuario:Ana@Loja.com", [0.5, 0.25, 0.125], CancellationToken.None);

        var amostras = await armazenamento.ObterAsync("usuario:ana@loja.com", CancellationToken.None);
        Assert.AreEqual(1, amostras.Count);
        CollectionAssert.AreEqual(new[] { 0.5, 0.25, 0.125 }, amostras[0].ToArray());

        var gravado = (await contexto.Set<AmostraFacial>().SingleAsync()).Conteudo;
        var textoGravado = System.Text.Encoding.UTF8.GetString(gravado);
        Assert.IsFalse(textoGravado.Contains("0.25"), "os landmarks não podem estar legíveis no banco");
    }

    [TestMethod]
    public async Task Armazenamento_GuardaSoAsTresMaisRecentes()
    {
        await using var contexto = NovoContexto(NovoBanco());
        var armazenamento = new ArmazenamentoFacialEmOrm(contexto, NovoCifrador((1, 7)));

        for (var i = 1; i <= 5; i++)
        {
            await armazenamento.SalvarAsync("usuario:a@b.com", [i], CancellationToken.None);
            await Task.Delay(5);
        }

        var amostras = await armazenamento.ObterAsync("usuario:a@b.com", CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 3.0, 4.0, 5.0 }, amostras.Select(a => a[0]).ToArray());
    }

    [TestMethod]
    public async Task Armazenamento_NaoMisturaPessoas()
    {
        await using var contexto = NovoContexto(NovoBanco());
        var armazenamento = new ArmazenamentoFacialEmOrm(contexto, NovoCifrador((1, 7)));
        await armazenamento.SalvarAsync("usuario:a@b.com", [1], CancellationToken.None);

        Assert.IsTrue(await armazenamento.ExisteAsync("usuario:a@b.com", CancellationToken.None));
        Assert.IsFalse(await armazenamento.ExisteAsync("usuario:b@b.com", CancellationToken.None));
        Assert.AreEqual(0, (await armazenamento.ObterAsync("usuario:b@b.com", CancellationToken.None)).Count);
    }

    [TestMethod]
    public async Task Armazenamento_RemoverApagaDeVerdadeEEhIdempotente()
    {
        await using var contexto = NovoContexto(NovoBanco());
        var armazenamento = new ArmazenamentoFacialEmOrm(contexto, NovoCifrador((1, 7)));
        await armazenamento.SalvarAsync("usuario:a@b.com", [1], CancellationToken.None);
        await armazenamento.SalvarAsync("usuario:a@b.com", [2], CancellationToken.None);

        Assert.IsTrue(await armazenamento.RemoverAsync("usuario:a@b.com", CancellationToken.None));
        Assert.IsFalse(await armazenamento.RemoverAsync("usuario:a@b.com", CancellationToken.None));
        Assert.AreEqual(0, await contexto.Set<AmostraFacial>().CountAsync(), "nenhuma linha pode restar (não é exclusão lógica)");
    }
}
