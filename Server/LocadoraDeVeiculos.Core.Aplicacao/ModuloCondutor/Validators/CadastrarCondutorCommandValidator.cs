using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Validators
{
    public class CadastrarCondutorCommandValidator : AbstractValidator<CadastrarCondutorCommand>
    {
        public CadastrarCondutorCommandValidator()
        {
            RuleFor(p => p.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(3).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(200).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .EmailAddress().WithMessage("O campo {PropertyName} deve conter um e-mail válido.")
                .MaximumLength(100).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Cpf)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$")
                .WithMessage("O campo {PropertyName} deve seguir o formato '000.000.000-00'.");

            RuleFor(p => p.Cnh)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(5).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(20).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.ValidadeCnh)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .GreaterThan(DateTime.Now).WithMessage("A CNH deve ter validade futura.");

            RuleFor(p => p.Telefone)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^\(\d{2}\) \d{4,5}-\d{4}$")
                .WithMessage("O campo {PropertyName} deve seguir o formato '(00) 00000-0000'.");
        }
    }
}