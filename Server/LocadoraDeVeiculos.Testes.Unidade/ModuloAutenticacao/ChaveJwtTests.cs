using LocadoraDeVeiculos.Infraestrutura.Orm.jwt;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAutenticacao;

// S17: a API não deve subir com uma chave de assinatura ausente ou curta demais.
[TestClass]
[TestCategory("Unidade - Chave JWT")]
public sealed class ChaveJwtTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Validar_SemChave_Falha(string? chave)
    {
        Assert.ThrowsException<ArgumentException>(() => ChaveJwt.Validar(chave));
    }

    [TestMethod]
    public void Validar_ChaveCurta_FalhaComMensagemClara()
    {
        var ex = Assert.ThrowsException<ArgumentException>(() => ChaveJwt.Validar(new string('a', ChaveJwt.TamanhoMinimo - 1)));

        StringAssert.Contains(ex.Message, "pelo menos 32");
    }

    [TestMethod]
    public void Validar_ChaveNoTamanhoMinimo_Passa()
    {
        var chave = new string('a', ChaveJwt.TamanhoMinimo);

        Assert.AreEqual(chave, ChaveJwt.Validar(chave));
    }
}
