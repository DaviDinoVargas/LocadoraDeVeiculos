using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

/// <summary>
/// Validador de senha do Identity que recusa senhas acima de <see cref="RegrasDeSenha.TamanhoMaximo"/> caracteres. Vale para TODOS os
/// caminhos que gravam senha (cadastro, troca, redefinição, funcionário), mesmo os que não passam por FluentValidation.
/// </summary>
public sealed class LimiteDeTamanhoDeSenha : IPasswordValidator<Usuario>
{
    public Task<IdentityResult> ValidateAsync(UserManager<Usuario> manager, Usuario user, string? password) =>
        Task.FromResult(password is { Length: > RegrasDeSenha.TamanhoMaximo }
            ? IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = $"A senha deve ter no máximo {RegrasDeSenha.TamanhoMaximo} caracteres."
            })
            : IdentityResult.Success);
}
