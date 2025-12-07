using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Validators
{
    public class EditarGrupoAutomovelCommandValidator : AbstractValidator<EditarGrupoAutomovelCommand>
    {
        public EditarGrupoAutomovelCommandValidator()
        {
            RuleFor(p => p.Nome)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(3).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(100).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Descricao)
                .MaximumLength(500).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");
        }
    }
}