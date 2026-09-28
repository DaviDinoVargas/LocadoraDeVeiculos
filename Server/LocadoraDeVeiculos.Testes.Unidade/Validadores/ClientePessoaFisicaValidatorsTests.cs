using FluentValidation.TestHelper;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Validators;
using System;

namespace LocadoraDeVeiculos.Testes.Unidade.Validadores;

[TestClass]
public sealed class ClientePessoaFisicaValidatorsTests
{
    private readonly CadastrarClientePessoaFisicaCommandValidator _validator = new();

    private static CadastrarClientePessoaFisicaCommand ComandoValido(
        string cpf = "123.456.789-00",
        string telefone = "(11) 91234-5678",
        string email = "cliente@teste.com",
        DateTime? validadeCnh = null) =>
        new(
            "Cliente Teste",
            cpf,
            "1234567",
            "CNH123456",
            validadeCnh ?? DateTime.Now.AddYears(2),
            telefone,
            email,
            "Rua das Flores, 123, Centro",
            null);

    [TestMethod]
    public void Cpf_SemFormatoEsperado_EhInvalido()
    {
        var command = ComandoValido(cpf: "12345678900");

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Cpf);
    }

    [TestMethod]
    public void Telefone_SemFormatoEsperado_EhInvalido()
    {
        var command = ComandoValido(telefone: "11912345678");

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Telefone);
    }

    [TestMethod]
    public void Email_Invalido_EhRecusado()
    {
        var command = ComandoValido(email: "nao-e-um-email");

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.Email);
    }

    [TestMethod]
    public void ValidadeCnh_Vencida_EhInvalida()
    {
        var command = ComandoValido(validadeCnh: DateTime.Now.AddDays(-1));

        var resultado = _validator.TestValidate(command);

        resultado.ShouldHaveValidationErrorFor(c => c.ValidadeCnh);
    }

    [TestMethod]
    public void Comando_ComTodosCamposValidos_NaoTemErros()
    {
        var command = ComandoValido();

        var resultado = _validator.TestValidate(command);

        resultado.ShouldNotHaveAnyValidationErrors();
    }
}
