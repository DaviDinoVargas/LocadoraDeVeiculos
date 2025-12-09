using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators
{
    public class CancelarAluguelCommandValidator : AbstractValidator<CancelarAluguelCommand>
    {
        public CancelarAluguelCommandValidator()
        {
            RuleFor(p => p.Id)
                .NotEmpty().WithMessage("O ID do aluguel é obrigatório.");
        }
    }
}