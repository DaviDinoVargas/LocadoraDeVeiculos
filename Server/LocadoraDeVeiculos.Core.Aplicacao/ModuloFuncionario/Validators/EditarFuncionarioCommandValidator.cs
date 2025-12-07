using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Validators;

public class EditarFuncionarioCommandValidator : AbstractValidator<EditarFuncionarioCommand>
{
    public EditarFuncionarioCommandValidator()
    {
        RuleFor(p => p.NomeCompleto)
            .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
            .MinimumLength(3).WithMessage("O campo {PropertyName} deve conter no mínimo {MinLength} caracteres.");

        RuleFor(p => p.Cpf)
            .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
            .Matches(@"^\d{3}\.\d{3}\.\d{3}-\d{2}$")
            .WithMessage("O campo {PropertyName} deve seguir o formato '000.000.000-00'.");

        RuleFor(p => p.Salario)
            .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
            .GreaterThan(0).WithMessage("O campo {PropertyName} precisa conter um valor maior que 0.");
    }
}
