using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Identity;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAutenticacao;

// Entrada hostil: texto do usuário que vai para o log e senhas gigantes.
[TestClass]
[TestCategory("Unidade - Entrada hostil")]
public sealed class EntradaHostilTests
{
    [TestMethod]
    [DataRow(0x0A)]     // \n
    [DataRow(0x0D)]     // \r
    [DataRow(0x2028)]   // separador de linha Unicode
    [DataRow(0x2029)]   // separador de parágrafo Unicode
    [DataRow(0x85)]     // NEL
    [DataRow(0x09)]     // tab
    [DataRow(0x00)]     // nulo
    public void MascararEmail_NaoDeixaPassarQuebraDeLinhaNemCaractereDeControle(int pontoDeCodigo)
    {
        var entrada = "x@y.com" + (char)pontoDeCodigo + "FORJADO Login OK";

        var saida = MascaramentoDeDados.MascararEmail(entrada);

        Assert.IsFalse(saida.Any(c => char.IsControl(c) || (int)c is 0x2028 or 0x2029), "saída com caractere de linha ou de controle");
        StringAssert.StartsWith(saida, "x***@");
    }

    [TestMethod]
    public void MascararEmail_LimitaOTamanho_ComportamentoNormalNaoMuda()
    {
        Assert.AreEqual("p***@dominio.com", MascaramentoDeDados.MascararEmail("pessoa@dominio.com"));
        Assert.AreEqual("(vazio)", MascaramentoDeDados.MascararEmail("  "));
        Assert.AreEqual("***", MascaramentoDeDados.MascararEmail("sem-arroba"));
        Assert.IsTrue(MascaramentoDeDados.MascararEmail("a@" + new string('b', 50_000)).Length < 200);
    }

    [TestMethod]
    public void Sanear_RemoveControleELimita()
    {
        Assert.AreEqual("ab", MascaramentoDeDados.Sanear("a\r\nb"));
        Assert.AreEqual(5, MascaramentoDeDados.Sanear("abcdefghij", 5).Length);
        Assert.AreEqual(string.Empty, MascaramentoDeDados.Sanear(null));
    }

    private static Task<IdentityResult> Validar(string senha) =>
        new LimiteDeTamanhoDeSenha().ValidateAsync(null!, new Usuario(), senha);

    [TestMethod]
    public async Task Senha_AteOLimite_Passa_AcimaDele_EhRecusada()
    {
        Assert.IsTrue((await Validar(new string('a', RegrasDeSenha.TamanhoMaximo))).Succeeded);

        var longa = await Validar(new string('a', RegrasDeSenha.TamanhoMaximo + 1));
        Assert.IsFalse(longa.Succeeded);
        Assert.AreEqual("PasswordTooLong", longa.Errors.Single().Code);
        Assert.IsFalse((await Validar(new string('A', 180_000))).Succeeded);
    }

    [TestMethod]
    public void RegraFluentValidation_TambemTemOTeto()
    {
        var validador = new FluentValidation.InlineValidator<string>();
        validador.RuleFor(s => s).SenhaForte();

        Assert.IsTrue(validador.Validate("Abcdefg1").IsValid);
        Assert.IsTrue(validador.Validate("Ab1" + new string('x', RegrasDeSenha.TamanhoMaximo - 3)).IsValid);
        Assert.IsFalse(validador.Validate("Ab1" + new string('x', RegrasDeSenha.TamanhoMaximo)).IsValid);
    }
}
