using FluentValidation;

public class CadastrarPlanoCobrancaCommandValidator : AbstractValidator<CadastrarPlanoCobrancaCommand>
{
    public CadastrarPlanoCobrancaCommandValidator()
    {
        RuleFor(p => p.Nome).NotEmpty().MinimumLength(3);
        RuleFor(p => p.PrecoDiaria).GreaterThan(0);
        RuleFor(p => p.PrecoPorKm).GreaterThanOrEqualTo(0);
        RuleFor(p => p.KmLivreLimite).GreaterThanOrEqualTo(0);
    }
}
