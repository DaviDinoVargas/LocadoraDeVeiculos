using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Validators
{
    public class CadastrarCupomCommandValidator : AbstractValidator<CadastrarCupomCommand>
    {
        public CadastrarCupomCommandValidator()
        {
            RuleFor(c => c.Codigo)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^[A-Za-z0-9]{3,30}$")
                .WithMessage("O campo {PropertyName} deve conter só letras e números (3 a 30 caracteres).");

            RuleFor(c => c.Descricao)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MaximumLength(300).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(c => c.TipoDesconto)
                .IsInEnum().WithMessage("O campo {PropertyName} é inválido.");

            RuleFor(c => c.ValorDesconto)
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.");

            RuleFor(c => c.ValorDesconto)
                .LessThanOrEqualTo(100)
                .When(c => c.TipoDesconto == TipoDesconto.Percentual)
                .WithMessage("Um desconto percentual não pode ser maior que 100%.");

            RuleFor(c => c.ValidoAte)
                .GreaterThan(DateTimeOffset.UtcNow)
                .WithMessage("O campo {PropertyName} deve ser uma data futura.");

            RuleFor(c => c.LimiteUsos)
                .GreaterThan(0)
                .When(c => c.LimiteUsos.HasValue)
                .WithMessage("O campo {PropertyName} deve ser maior que zero quando informado.");
        }
    }

    public class EditarCupomCommandValidator : AbstractValidator<EditarCupomCommand>
    {
        public EditarCupomCommandValidator()
        {
            RuleFor(c => c.Codigo)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Matches(@"^[A-Za-z0-9]{3,30}$")
                .WithMessage("O campo {PropertyName} deve conter só letras e números (3 a 30 caracteres).");

            RuleFor(c => c.Descricao)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MaximumLength(300).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(c => c.TipoDesconto)
                .IsInEnum().WithMessage("O campo {PropertyName} é inválido.");

            RuleFor(c => c.ValorDesconto)
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.");

            RuleFor(c => c.ValorDesconto)
                .LessThanOrEqualTo(100)
                .When(c => c.TipoDesconto == TipoDesconto.Percentual)
                .WithMessage("Um desconto percentual não pode ser maior que 100%.");

            RuleFor(c => c.LimiteUsos)
                .GreaterThan(0)
                .When(c => c.LimiteUsos.HasValue)
                .WithMessage("O campo {PropertyName} deve ser maior que zero quando informado.");
        }
    }
}
