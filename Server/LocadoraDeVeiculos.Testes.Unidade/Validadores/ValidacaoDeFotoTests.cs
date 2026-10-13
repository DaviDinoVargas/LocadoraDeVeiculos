using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

// S31: a foto do veículo vai para um <img src>; só URL https ou imagem embutida pequena.
[TestClass]
[TestCategory("Unidade - Foto do veículo")]
public sealed class ValidacaoDeFotoTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("https://exemplo.com/onix.jpg")]
    [DataRow("HTTPS://exemplo.com/onix.jpg?v=2")]
    [DataRow("data:image/png;base64,iVBORw0KGgo=")]
    [DataRow("data:image/jpeg;base64,/9j/4AAQSkZJRg==")]
    public void Foto_Valida(string? foto) => Assert.IsTrue(ValidacaoDeFoto.FotoValida(foto));

    [TestMethod]
    [DataRow("javascript:alert(1)")]
    [DataRow("JaVaScRiPt:alert(1)")]
    [DataRow("file:///c:/windows/win.ini")]
    [DataRow("http://exemplo.com/onix.jpg")]
    [DataRow("ftp://exemplo.com/onix.jpg")]
    [DataRow("//exemplo.com/onix.jpg")]
    [DataRow("/relativo/onix.jpg")]
    [DataRow("https://usuario:senha@exemplo.com/onix.jpg")]
    [DataRow("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [DataRow("data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+")]
    [DataRow("data:image/png;base64,@@@")]
    public void Foto_Invalida(string foto) => Assert.IsFalse(ValidacaoDeFoto.FotoValida(foto));

    [TestMethod]
    public void Foto_ComUrlOuImagemGrandesDemais_Invalida()
    {
        Assert.IsFalse(ValidacaoDeFoto.FotoValida("https://exemplo.com/" + new string('a', ValidacaoDeFoto.TamanhoMaximoDaUrl)));
        Assert.IsFalse(ValidacaoDeFoto.FotoValida("data:image/png;base64," + new string('A', ValidacaoDeFoto.TamanhoMaximoDaImagemEmbutida)));
        Assert.IsTrue(ValidacaoDeFoto.FotoValida("data:image/png;base64," + new string('A', 1000)));
    }
}
