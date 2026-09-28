using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class AluguelValidatorsTests
{
    private readonly CadastrarAluguelCommandValidator _validator = new();

    private static CadastrarAluguelCommand ComandoValido(
        DateTimeOffset? dataSaida = null,
        DateTimeOffset? dataRetornoPrevisto = null,
        decimal valorPrevisto = 300m) =>
        new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            dataSaida ?? DateTimeOffset.Now.AddHours(1),
            dataRetornoPrevisto ?? DateTimeOffset.Now.AddDays(3),
            valorPrevisto,
            new List<Guid>(),
            null);

    [TestMethod]
    public void DataRetornoPrevisto_AntesDaDataSaida_EhInvalida()
    {
        var saida = DateTimeOffset.Now.AddDays(2);
        var command = ComandoValido(dataSaida: saida, dataRetornoPrevisto: saida.AddDays(-1));

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.DataRetornoPrevisto);
    }

    [TestMethod]
    public void DataRetornoPrevisto_DepoisDaDataSaida_EhValida()
    {
        var saida = DateTimeOffset.Now.AddDays(1);
        var command = ComandoValido(dataSaida: saida, dataRetornoPrevisto: saida.AddDays(2));

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveValidationErrorFor(c => c.DataRetornoPrevisto);
    }

    [TestMethod]
    public void DataSaida_MuitoNoPassado_EhInvalida()
    {
        var command = ComandoValido(dataSaida: DateTimeOffset.Now.AddDays(-5), dataRetornoPrevisto: DateTimeOffset.Now.AddDays(-1));

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.DataSaida);
    }

    [TestMethod]
    public void ValorPrevisto_ZeroOuNegativo_EhInvalido()
    {
        var command = ComandoValido(valorPrevisto: 0m);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.ValorPrevisto);
    }
}
