using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Validators
{
    public class EditarTaxaServicoCommandValidator : AbstractValidator<EditarTaxaServicoCommand>
    {
        public EditarTaxaServicoCommandValidator()
        {
            RuleFor(p => p.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(3).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(100).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Preco)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.")
                .PrecisionScale(18, 2, false).WithMessage("O campo {PropertyName} deve ter no máximo 18 dígitos com 2 casas decimais.");

            RuleFor(p => p.TipoCalculo)
                .IsInEnum().WithMessage("O campo {PropertyName} é inválido.");
        }
    }
}