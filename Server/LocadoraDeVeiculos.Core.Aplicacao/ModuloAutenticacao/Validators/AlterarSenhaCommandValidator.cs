using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Validators;

public class AlterarSenhaCommandValidator : AbstractValidator<AlterarSenhaCommand>
{
    public AlterarSenhaCommandValidator()
    {
        RuleFor(p => p.SenhaAtual).NotEmpty().WithMessage("Informe a senha atual.");

        RuleFor(p => p.NovaSenha)
            .SenhaForte()
            .NotEqual(p => p.SenhaAtual).WithMessage("A nova senha precisa ser diferente da atual.");

        RuleFor(p => p.ConfirmarNovaSenha)
            .Equal(p => p.NovaSenha).WithMessage("A confirmação da nova senha não confere.");
    }
}
