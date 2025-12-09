using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Validators
{
    public class RegistrarDevolucaoCommandValidator : AbstractValidator<RegistrarDevolucaoCommand>
    {
        public RegistrarDevolucaoCommandValidator()
        {
            RuleFor(p => p.AluguelId)
                .NotEmpty().WithMessage("O campo Aluguel é obrigatório.");

            RuleFor(p => p.DataDevolucao)
                .NotEmpty().WithMessage("O campo Data de Devolução é obrigatório.");

            RuleFor(p => p.QuilometragemFinal)
                .NotEmpty().WithMessage("O campo Quilometragem Final é obrigatório.")
                .GreaterThan(0).WithMessage("A quilometragem final deve ser maior que zero.");

            RuleFor(p => p.CombustivelNoTanque)
                .NotEmpty().WithMessage("O campo Combustível no Tanque é obrigatório.")
                .GreaterThanOrEqualTo(0).WithMessage("O combustível no tanque não pode ser negativo.");

            RuleFor(p => p.NivelCombustivel)
                .IsInEnum().WithMessage("O campo Nível de Combustível é inválido.");
        }
    }
}