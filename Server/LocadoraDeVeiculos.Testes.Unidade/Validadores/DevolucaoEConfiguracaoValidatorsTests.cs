using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Validators;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using System;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class DevolucaoEConfiguracaoValidatorsTests
{
    [TestMethod]
    public void RegistrarDevolucao_QuilometragemZeroOuNegativa_EhInvalida()
    {
        var validator = new RegistrarDevolucaoCommandValidator();
        var command = new RegistrarDevolucaoCommand(Guid.NewGuid(), DateTimeOffset.Now, 0m, 30m, NivelCombustivel.Metade);

        var resultado = validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.QuilometragemFinal);
    }

    [TestMethod]
    public void RegistrarDevolucao_CombustivelNegativo_EhInvalido()
    {
        var validator = new RegistrarDevolucaoCommandValidator();
        var command = new RegistrarDevolucaoCommand(Guid.NewGuid(), DateTimeOffset.Now, 100m, -5m, NivelCombustivel.Metade);

        var resultado = validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.CombustivelNoTanque);
    }

    [TestMethod]
    public void RegistrarDevolucao_ComCamposValidos_NaoTemErros()
    {
        var validator = new RegistrarDevolucaoCommandValidator();
        var command = new RegistrarDevolucaoCommand(Guid.NewGuid(), DateTimeOffset.Now, 15000m, 40m, NivelCombustivel.TresQuartos);

        var resultado = validator.TestValidate(command);

        resultado.ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public void ConfigurarPrecoCombustivel_ZeroOuNegativo_EhInvalido()
    {
        var validator = new ConfigurarPrecoCombustivelCommandValidator();
        var command = new ConfigurarPrecoCombustivelCommand(0m);

        var resultado = validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.PrecoCombustivel);
    }

    [TestMethod]
    public void ConfigurarPrecoCombustivel_Positivo_EhValido()
    {
        var validator = new ConfigurarPrecoCombustivelCommandValidator();
        var command = new ConfigurarPrecoCombustivelCommand(5.89m);

        var resultado = validator.TestValidate(command);

        resultado.ShouldNotHaveValidationErrorFor(c => c.PrecoCombustivel);
    }
}
