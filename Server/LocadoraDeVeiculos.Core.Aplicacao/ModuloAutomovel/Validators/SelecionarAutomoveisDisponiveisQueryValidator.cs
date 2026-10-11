using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Validators
{
    public class SelecionarAutomoveisDisponiveisQueryValidator : AbstractValidator<SelecionarAutomoveisDisponiveisQuery>
    {
        public SelecionarAutomoveisDisponiveisQueryValidator()
        {
            RuleFor(p => p.DataSaida)
                .NotEmpty().WithMessage("O campo Data de Saída é obrigatório.");

            RuleFor(p => p.DataRetornoPrevisto)
                .NotEmpty().WithMessage("O campo Data de Retorno Previsto é obrigatório.")
                .GreaterThan(p => p.DataSaida)
                .WithMessage("A data de retorno previsto deve ser após a data de saída.");
        }
    }
}
