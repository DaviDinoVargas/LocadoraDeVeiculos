using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class ParceiroValidatorsTests
{
    private readonly CadastrarParceiroCommandValidator _validator = new();

    [TestMethod]
    public void Cnpj_SemFormatoEsperado_EhInvalido()
    {
        var command = new CadastrarParceiroCommand("Posto Central", "12345678000190", CategoriaParceiro.PostoDeCombustivel);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Cnpj);
    }

    [TestMethod]
    public void Cnpj_ComFormatoEsperado_EhValido()
    {
        var command = new CadastrarParceiroCommand("Posto Central", "12.345.678/0001-90", CategoriaParceiro.PostoDeCombustivel);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveValidationErrorFor(c => c.Cnpj);
    }

    [TestMethod]
    public void Nome_MenorQueDoisCaracteres_EhInvalido()
    {
        var command = new CadastrarParceiroCommand("A", "12.345.678/0001-90", CategoriaParceiro.Hotel);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Nome);
    }

    [TestMethod]
    public void Categoria_ForaDoEnum_EhInvalida()
    {
        var command = new CadastrarParceiroCommand("Posto Central", "12.345.678/0001-90", (CategoriaParceiro)999);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Categoria);
    }
}
