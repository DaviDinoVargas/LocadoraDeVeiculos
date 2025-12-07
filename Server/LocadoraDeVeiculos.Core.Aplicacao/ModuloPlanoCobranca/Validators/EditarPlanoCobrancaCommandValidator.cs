using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Validators
{
    public class EditarPlanoCobrancaCommandValidator : AbstractValidator<EditarPlanoCobrancaCommand>
    {
        public EditarPlanoCobrancaCommandValidator()
        {
            RuleFor(p => p.Nome).NotEmpty().MinimumLength(3);
            RuleFor(p => p.PrecoDiaria).GreaterThan(0);
            RuleFor(p => p.PrecoPorKm).GreaterThanOrEqualTo(0);
            RuleFor(p => p.KmLivreLimite).GreaterThanOrEqualTo(0);
        }
    }
}
