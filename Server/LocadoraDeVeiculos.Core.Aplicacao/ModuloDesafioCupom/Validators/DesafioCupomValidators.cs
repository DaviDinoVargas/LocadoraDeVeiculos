using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Validators
{
    public class CadastrarDesafioCupomCommandValidator : AbstractValidator<CadastrarDesafioCupomCommand>
    {
        public CadastrarDesafioCupomCommandValidator()
        {
            RuleFor(d => d.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MaximumLength(150).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(d => d.Descricao)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MaximumLength(300).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(d => d.MetaQuantidadeAlugueis)
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.");

            RuleFor(d => d.PeriodoDias)
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.");

            RuleFor(d => d.CupomRecompensaId)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.");
        }
    }

    public class EditarDesafioCupomCommandValidator : AbstractValidator<EditarDesafioCupomCommand>
    {
        public EditarDesafioCupomCommandValidator()
        {
            RuleFor(d => d.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MaximumLength(150).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(d => d.Descricao)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MaximumLength(300).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(d => d.MetaQuantidadeAlugueis)
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.");

            RuleFor(d => d.PeriodoDias)
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.");

            RuleFor(d => d.CupomRecompensaId)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.");
        }
    }
}
