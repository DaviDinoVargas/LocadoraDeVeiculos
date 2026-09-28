using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Validators;
using System;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class DesafioCupomValidatorsTests
{
    private readonly CadastrarDesafioCupomCommandValidator _validator = new();

    private static CadastrarDesafioCupomCommand ComandoValido(
        int metaQuantidadeAlugueis = 5,
        int periodoDias = 30,
        Guid? cupomRecompensaId = null) =>
        new("Desafio de fidelidade", "Complete 5 aluguéis em 30 dias", metaQuantidadeAlugueis, periodoDias, cupomRecompensaId ?? Guid.NewGuid());

    [TestMethod]
    public void MetaQuantidadeAlugueis_ZeroOuNegativa_EhInvalida()
    {
        var command = ComandoValido(metaQuantidadeAlugueis: 0);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(d => d.MetaQuantidadeAlugueis);
    }

    [TestMethod]
    public void PeriodoDias_ZeroOuNegativo_EhInvalido()
    {
        var command = ComandoValido(periodoDias: -5);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(d => d.PeriodoDias);
    }

    [TestMethod]
    public void CupomRecompensaId_Vazio_EhInvalido()
    {
        var command = ComandoValido(cupomRecompensaId: Guid.Empty);

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(d => d.CupomRecompensaId);
    }

    [TestMethod]
    public void Comando_ComTodosCamposValidos_NaoTemErros()
    {
        var command = ComandoValido();

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveAnyValidationErrors();
    }
}
