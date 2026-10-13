using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

// S13: o dígito verificador de CPF/CNPJ passa a ser conferido também no servidor.
[TestClass]
[TestCategory("Unidade - Documentos")]
public sealed class ValidacaoDeDocumentosTests
{
    [TestMethod]
    [DataRow("529.982.247-25")]
    [DataRow("52998224725")]
    [DataRow("168.995.350-09")]
    public void Cpf_Valido(string cpf) => Assert.IsTrue(ValidacaoDeDocumentos.CpfValido(cpf));

    [TestMethod]
    [DataRow("529.982.247-26")]
    [DataRow("111.111.111-11")]
    [DataRow("123.456.789-00")]
    [DataRow("529.982.247")]
    [DataRow("")]
    [DataRow(null)]
    public void Cpf_Invalido(string? cpf) => Assert.IsFalse(ValidacaoDeDocumentos.CpfValido(cpf));

    [TestMethod]
    [DataRow("11.222.333/0001-81")]
    [DataRow("11222333000181")]
    [DataRow("45.997.418/0001-53")]
    public void Cnpj_Valido(string cnpj) => Assert.IsTrue(ValidacaoDeDocumentos.CnpjValido(cnpj));

    [TestMethod]
    [DataRow("11.222.333/0001-82")]
    [DataRow("00.000.000/0000-00")]
    [DataRow("12.345.678/0001-90")]
    [DataRow("")]
    [DataRow(null)]
    public void Cnpj_Invalido(string? cnpj) => Assert.IsFalse(ValidacaoDeDocumentos.CnpjValido(cnpj));
}
