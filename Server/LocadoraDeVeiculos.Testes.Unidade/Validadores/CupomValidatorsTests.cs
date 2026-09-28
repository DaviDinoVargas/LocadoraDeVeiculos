using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using System;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class CupomValidatorsTests
{
    private readonly CadastrarCupomCommandValidator _validator = new();

    private static CadastrarCupomCommand ComandoValido(
        TipoDesconto tipo = TipoDesconto.Percentual,
        decimal valorDesconto = 10m,
        DateTimeOffset? validoAte = null,
        int? limiteUsos = 10) =>
        new("PROMO10", "Cupom de teste", tipo, valorDesconto, validoAte ?? DateTimeOffset.UtcNow.AddDays(30), limiteUsos, null);

    [TestMethod]
    public void DescontoPercentual_MaiorQueCem_EhInvalido()
    {
        var command = ComandoValido(TipoDesconto.Percentual, valorDesconto: 150m);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.ValorDesconto);
    }

    [TestMethod]
    public void DescontoValorFixo_MaiorQueCem_EhValido()
    {
        // Só o desconto percentual tem teto de 100 -- um cupom de R$ 150 fixos é normal.
        var command = ComandoValido(TipoDesconto.ValorFixo, valorDesconto: 150m);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveValidationErrorFor(c => c.ValorDesconto);
    }

    [TestMethod]
    public void ValorDesconto_ZeroOuNegativo_EhInvalido()
    {
        var command = ComandoValido(valorDesconto: 0m);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.ValorDesconto);
    }

    [TestMethod]
    public void ValidoAte_DataPassada_EhInvalido()
    {
        var command = ComandoValido(validoAte: DateTimeOffset.UtcNow.AddDays(-1));

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.ValidoAte);
    }

    [TestMethod]
    public void Codigo_ComCaracteresEspeciais_EhInvalido()
    {
        var command = ComandoValido() with { Codigo = "PROMO-10%" };

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Codigo);
    }

    [TestMethod]
    public void LimiteUsos_NuloEhPermitido_UsoIlimitado()
    {
        var command = ComandoValido(limiteUsos: null);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveValidationErrorFor(c => c.LimiteUsos);
    }

    [TestMethod]
    public void LimiteUsos_ZeroQuandoInformado_EhInvalido()
    {
        var command = ComandoValido(limiteUsos: 0);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.LimiteUsos);
    }
}
