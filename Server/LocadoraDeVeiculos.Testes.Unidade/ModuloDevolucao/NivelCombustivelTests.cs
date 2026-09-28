using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloDevolucao;

// Regressão: o Angular sempre mandou "Metade" pro nível de combustível, mas o enum se
// chamava "Meio" -- DevolucaoController faz Enum.TryParse<NivelCombustivel> direto na
// string recebida, então toda devolução com o tanque pela metade era recusada com 400
// "Nível de combustível inválido.". Ver Devolucao.cs (o enum foi renomeado).
[TestClass]
public sealed class NivelCombustivelTests
{
    [TestMethod]
    public void TryParse_ComOValorQueOAngularEnvia_Reconhece()
    {
        var conseguiu = System.Enum.TryParse<NivelCombustivel>("Metade", out var nivel);

        Assert.IsTrue(conseguiu);
        Assert.AreEqual(NivelCombustivel.Metade, nivel);
        Assert.AreEqual(50, (int)nivel);
    }

    [DataTestMethod]
    [DataRow("Vazio", 0)]
    [DataRow("UmQuarto", 25)]
    [DataRow("Metade", 50)]
    [DataRow("TresQuartos", 75)]
    [DataRow("Cheio", 100)]
    public void TryParse_ComQualquerOpcaoDoDropdownDoAngular_Reconhece(string valorEnviado, int valorEsperado)
    {
        var conseguiu = System.Enum.TryParse<NivelCombustivel>(valorEnviado, out var nivel);

        Assert.IsTrue(conseguiu, $"'{valorEnviado}' deveria ser reconhecido pelo enum NivelCombustivel.");
        Assert.AreEqual(valorEsperado, (int)nivel);
    }
}
