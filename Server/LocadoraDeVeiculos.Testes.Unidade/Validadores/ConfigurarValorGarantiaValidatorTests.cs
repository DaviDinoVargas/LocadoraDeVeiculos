using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Validators;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class ConfigurarValorGarantiaValidatorTests
{
    private readonly ConfigurarValorGarantiaCommandValidator validator = new();

    [TestMethod]
    public void Negativo_EhInvalido()
    {
        validator.TestValidate(new ConfigurarValorGarantiaCommand(-0.01m))
            .ShouldHaveValidationErrorFor(c => c.ValorGarantia);
    }

    [TestMethod]
    public void Zero_EhValido_EmpresaPodeNaoCobrarCaucao()
    {
        validator.TestValidate(new ConfigurarValorGarantiaCommand(0m)).ShouldNotHaveAnyValidationErrors();
    }

    [TestMethod]
    public void AcimaDoLimite_EhInvalido_ProtegeContraErroDeDigitacao()
    {
        validator.TestValidate(new ConfigurarValorGarantiaCommand(ConfigurarValorGarantiaCommandValidator.LimiteMaximo + 1))
            .ShouldHaveValidationErrorFor(c => c.ValorGarantia);
    }

    [TestMethod]
    public void ValorNormal_EhValido()
    {
        validator.TestValidate(new ConfigurarValorGarantiaCommand(1500m)).ShouldNotHaveAnyValidationErrors();
    }
}
