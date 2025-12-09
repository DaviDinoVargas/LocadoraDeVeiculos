using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Validators
{
    public class ConfigurarPrecoCombustivelCommandValidator : AbstractValidator<ConfigurarPrecoCombustivelCommand>
    {
        public ConfigurarPrecoCombustivelCommandValidator()
        {
            RuleFor(p => p.PrecoCombustivel)
                .NotEmpty().WithMessage("O campo Preço do Combustível é obrigatório.")
                .GreaterThan(0).WithMessage("O preço do combustível deve ser maior que zero.");
        }
    }
}