using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Validators
{
    public class ConfigurarValorGarantiaCommandValidator : AbstractValidator<ConfigurarValorGarantiaCommand>
    {
        // Teto só para barrar erro de digitação (ex.: 1000000 em vez de 1000,00).
        public const decimal LimiteMaximo = 100_000m;

        public ConfigurarValorGarantiaCommandValidator()
        {
            RuleFor(p => p.ValorGarantia)
                .GreaterThanOrEqualTo(0).WithMessage("O valor da garantia não pode ser negativo.")
                .LessThanOrEqualTo(LimiteMaximo).WithMessage("O valor da garantia não pode passar de R$ 100.000,00.");
        }
    }
}
