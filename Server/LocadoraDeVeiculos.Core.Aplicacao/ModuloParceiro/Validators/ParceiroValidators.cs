using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Validators
{
    public class CadastrarParceiroCommandValidator : AbstractValidator<CadastrarParceiroCommand>
    {
        public CadastrarParceiroCommandValidator()
        {
            RuleFor(p => p.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(2).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(150).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Cnpj)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$")
                .WithMessage("O campo {PropertyName} deve seguir o formato '00.000.000/0000-00'.");

            RuleFor(p => p.Categoria)
                .IsInEnum().WithMessage("O campo {PropertyName} é inválido.");
        }
    }

    public class EditarParceiroCommandValidator : AbstractValidator<EditarParceiroCommand>
    {
        public EditarParceiroCommandValidator()
        {
            RuleFor(p => p.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(2).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(150).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Cnpj)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}$")
                .WithMessage("O campo {PropertyName} deve seguir o formato '00.000.000/0000-00'.");

            RuleFor(p => p.Categoria)
                .IsInEnum().WithMessage("O campo {PropertyName} é inválido.");
        }
    }
}
