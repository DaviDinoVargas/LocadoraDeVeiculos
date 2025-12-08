using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Validators
{
    public class CadastrarAutomovelCommandValidator : AbstractValidator<CadastrarAutomovelCommand>
    {
        public CadastrarAutomovelCommandValidator()
        {
            RuleFor(p => p.Placa)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .Length(7).WithMessage("O campo {PropertyName} deve conter {MaxLength} caracteres.")
                .Matches(@"^[A-Z]{3}[0-9][A-Z0-9][0-9]{2}$")
                .WithMessage("O campo {PropertyName} deve seguir o formato 'ABC1D23'.");

            RuleFor(p => p.Marca)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(2).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(50).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Cor)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(3).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(50).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.Modelo)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .MinimumLength(2).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.")
                .MaximumLength(100).WithMessage("O campo {PropertyName} deve conter no máximo {MaxLength} caracteres.");

            RuleFor(p => p.TipoCombustivel)
                .IsInEnum().WithMessage("O campo {PropertyName} é inválido.");

            RuleFor(p => p.CapacidadeTanque)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .GreaterThan(0).WithMessage("O campo {PropertyName} deve ser maior que zero.");

            RuleFor(p => p.Ano)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
                .InclusiveBetween(1900, DateTime.Now.Year + 1)
                .WithMessage("O campo {PropertyName} deve estar entre {From} e {To}.");

            RuleFor(p => p.GrupoAutomovelId)
                .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.");
        }
    }
}