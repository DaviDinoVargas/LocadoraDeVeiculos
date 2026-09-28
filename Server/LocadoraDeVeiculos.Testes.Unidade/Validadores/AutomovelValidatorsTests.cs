using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using System;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class AutomovelValidatorsTests
{
    private readonly CadastrarAutomovelCommandValidator _validator = new();

    private static CadastrarAutomovelCommand ComandoValido(string placa = "ABC1D23", int ano = 2023) =>
        new(placa, "Fiat", "Prata", "Uno", TipoCombustivel.Flex, 50m, ano, null, Guid.NewGuid());

    [TestMethod]
    public void Placa_ComTraco_EhInvalida()
    {
        var command = ComandoValido(placa: "ABC-1D2"); // o formato aceito não tem separador

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Placa);
    }

    [TestMethod]
    public void Placa_ComMenosDeSeteCaracteres_EhInvalida()
    {
        var command = ComandoValido(placa: "ABC1D2");

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Placa);
    }

    [TestMethod]
    public void Placa_NoPadraoMercosul_EhValida()
    {
        var command = ComandoValido(placa: "ABC1D23");

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveValidationErrorFor(c => c.Placa);
    }

    [TestMethod]
    public void Placa_NoPadraoAntigoTodoNumerico_TambemEhAceita()
    {
        // O regex do validador ('[A-Z0-9]' na 5a posição) aceita tanto Mercosul
        // (letra na 5a posição, ex. ABC1D23) quanto o formato antigo totalmente
        // numérico depois das 3 letras (ex. ABC1234) -- documentando esse
        // comportamento de propósito, não é um bug.
        var command = ComandoValido(placa: "ABC1234");

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveValidationErrorFor(c => c.Placa);
    }

    [TestMethod]
    public void Ano_NoFuturoDistante_EhInvalido()
    {
        var command = ComandoValido(ano: DateTime.Now.Year + 5);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Ano);
    }

    [TestMethod]
    public void Ano_AnteriorA1900_EhInvalido()
    {
        var command = ComandoValido(ano: 1899);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Ano);
    }

    [TestMethod]
    public void TipoCombustivel_ForaDoEnum_EhInvalido()
    {
        var command = ComandoValido() with { TipoCombustivel = (TipoCombustivel)999 };

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.TipoCombustivel);
    }
}
