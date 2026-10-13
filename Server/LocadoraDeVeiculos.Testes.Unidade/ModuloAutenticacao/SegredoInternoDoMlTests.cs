using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using Microsoft.Extensions.Configuration;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAutenticacao;

// S2: o valor padrão público do token interno só vale em desenvolvimento.
[TestClass]
[TestCategory("Unidade - Segredo interno do ML")]
public sealed class SegredoInternoDoMlTests
{
    private const string Forte = "um-segredo-bem-longo-com-mais-de-32-caracteres";

    private static IConfiguration Config(string? ambiente, string? token)
    {
        var valores = new Dictionary<string, string?>();

        if (ambiente is not null)
            valores["ASPNETCORE_ENVIRONMENT"] = ambiente;

        if (token is not null)
            valores[SegredoInternoDoMl.ChaveDeConfiguracao] = token;

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [TestMethod]
    public void Desenvolvimento_SemToken_UsaOValorPadrao()
    {
        Assert.AreEqual(SegredoInternoDoMl.ValorDeDesenvolvimento, SegredoInternoDoMl.Resolver(Config("Development", null)));
    }

    [TestMethod]
    public void Desenvolvimento_ComToken_UsaOInformado()
    {
        Assert.AreEqual("curto", SegredoInternoDoMl.Resolver(Config("Development", "curto")));
    }

    [TestMethod]
    [DataRow("Production", null)]
    [DataRow("Production", "")]
    [DataRow("Production", SegredoInternoDoMl.ValorDeDesenvolvimento)]
    [DataRow(null, null)]
    [DataRow("Staging", "curto")]
    public void ForaDeDesenvolvimento_RecusaTokenAusentePadraoOuCurto(string? ambiente, string? token)
    {
        Assert.ThrowsException<InvalidOperationException>(() => SegredoInternoDoMl.Resolver(Config(ambiente, token)));
    }

    [TestMethod]
    public void ForaDeDesenvolvimento_AceitaTokenLongo()
    {
        Assert.AreEqual(Forte, SegredoInternoDoMl.Resolver(Config("Production", Forte)));
    }
}
