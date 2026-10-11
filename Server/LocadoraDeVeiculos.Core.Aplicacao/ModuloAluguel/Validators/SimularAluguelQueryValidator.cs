using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators
{
    public class SimularAluguelQueryValidator : AbstractValidator<SimularAluguelQuery>
    {
        public SimularAluguelQueryValidator()
        {
            RuleFor(p => p.AutomovelId)
                .NotEmpty().WithMessage("O campo Automóvel é obrigatório.");

            RuleFor(p => p.DataSaida)
                .NotEmpty().WithMessage("O campo Data de Saída é obrigatório.");

            RuleFor(p => p.DataRetornoPrevisto)
                .NotEmpty().WithMessage("O campo Data de Retorno Previsto é obrigatório.")
                .GreaterThan(p => p.DataSaida)
                .WithMessage("A data de retorno previsto deve ser após a data de saída.");
        }
    }
}
