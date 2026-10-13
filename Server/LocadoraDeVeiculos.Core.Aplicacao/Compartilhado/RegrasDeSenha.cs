using FluentValidation;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

/// <summary>Política de senha do servidor (a mesma do Identity em IdentifyConfig), em um lugar só.</summary>
public static class RegrasDeSenha
{
    /// <summary>Teto do tamanho da senha (o mesmo no Identity, nos validators e no front): evita enviar centenas de milhares de caracteres ao hash.</summary>
    public const int TamanhoMaximo = 128;

    public static IRuleBuilderOptions<T, string> SenhaForte<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty().WithMessage("O campo {PropertyName} é obrigatório.")
            .MinimumLength(8).WithMessage("A senha deve conter pelo menos {MinLength} caracteres.")
            .MaximumLength(TamanhoMaximo).WithMessage("A senha deve ter no máximo {MaxLength} caracteres.")
            .Matches(@"[A-Z]").WithMessage("A senha deve conter pelo menos uma letra maiúscula.")
            .Matches(@"[a-z]").WithMessage("A senha deve conter pelo menos uma letra minúscula.")
            .Matches(@"[0-9]").WithMessage("A senha deve conter pelo menos um número.");
}
