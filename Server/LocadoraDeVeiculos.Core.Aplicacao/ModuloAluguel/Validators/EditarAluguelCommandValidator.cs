using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators
{
    public class EditarAluguelCommandValidator : AbstractValidator<EditarAluguelCommand>
    {
        public EditarAluguelCommandValidator()
        {
            RuleFor(p => p.CondutorId)
                .NotEmpty().WithMessage("O campo Condutor é obrigatório.");

            RuleFor(p => p.AutomovelId)
                .NotEmpty().WithMessage("O campo Automóvel é obrigatório.");

            RuleFor(p => p.ClienteId)
                .NotEmpty().WithMessage("O campo Cliente é obrigatório.");

            RuleFor(p => p.DataSaida)
                .NotEmpty().WithMessage("O campo Data de Saída é obrigatório.")
                .GreaterThan(DateTimeOffset.Now.AddHours(-1))
                .WithMessage("A data de saída deve ser futura.");

            RuleFor(p => p.DataRetornoPrevisto)
                .NotEmpty().WithMessage("O campo Data de Retorno Previsto é obrigatório.")
                .GreaterThan(p => p.DataSaida)
                .WithMessage("A data de retorno previsto deve ser após a data de saída.");

            RuleFor(p => p.ValorPrevisto)
                .NotEmpty().WithMessage("O campo Valor Previsto é obrigatório.")
                .GreaterThan(0).WithMessage("O valor previsto deve ser maior que zero.");
        }
    }
}