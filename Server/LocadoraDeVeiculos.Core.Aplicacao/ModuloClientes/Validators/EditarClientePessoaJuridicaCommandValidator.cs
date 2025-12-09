using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Validators
{
    public class EditarClientePessoaJuridicaCommandValidator : AbstractValidator<EditarClientePessoaJuridicaCommand>
    {
        public EditarClientePessoaJuridicaCommandValidator()
        {
            RuleFor(p => p.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(3).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(200).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Cnpj)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$")
                .WithMessage("O campo {PropertyName} deve seguir o formato '00.000.000/0000-00'.");

            RuleFor(p => p.NomeFantasia)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(3).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(200).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Telefone)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^\(\d{2}\) \d{4,5}-\d{4}$")
                .WithMessage("O campo {PropertyName} deve seguir o formato '(00) 00000-0000'.");

            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .EmailAddress().WithMessage("O campo {PropertyName} deve conter um e-mail válido.")
                .MaximumLength(100).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Endereco)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(10).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(500).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");
        }
    }
}