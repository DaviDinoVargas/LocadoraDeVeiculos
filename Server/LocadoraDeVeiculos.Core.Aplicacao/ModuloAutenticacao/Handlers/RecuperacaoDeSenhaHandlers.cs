using FluentResults;
using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

public class SolicitarRecuperacaoDeSenhaCommandHandler(FilaDeRecuperacaoDeSenha fila)
    : IRequestHandler<SolicitarRecuperacaoDeSenhaCommand, Result>
{
    public Task<Result> Handle(SolicitarRecuperacaoDeSenhaCommand command, CancellationToken cancellationToken)
    {
        // E-mail com cara de e-mail entra na fila; qualquer outra coisa é ignorada. Em ambos os casos a resposta é a mesma.
        if (!string.IsNullOrWhiteSpace(command.Email) && command.Email.Length <= 254 && command.Email.Contains('@'))
            fila.Enfileirar(command.Email);

        return Task.FromResult(Result.Ok());
    }
}

public class RedefinirSenhaCommandHandler(
    UserManager<Usuario> userManager,
    RefreshTokenProvider refreshTokenProvider,
    ILogger<RedefinirSenhaCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<RedefinirSenhaCommand, Result>
{
    // Mesma mensagem para e-mail inexistente, conta encerrada, token errado e token vencido.
    private const string LinkInvalido = "Link inválido ou expirado. Peça uma nova redefinição de senha.";

    public async Task<Result> Handle(RedefinirSenhaCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var validador = new InlineValidator<RedefinirSenhaCommand>();
            validador.RuleFor(c => c.NovaSenha).SenhaForte();
            validador.RuleFor(c => c.ConfirmarNovaSenha).Equal(c => c.NovaSenha).WithMessage("A confirmação da nova senha não confere.");

            var validacao = await validador.ValidateAsync(command, cancellationToken);

            if (!validacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(validacao.Errors.Select(e => e.ErrorMessage)));

            var usuario = string.IsNullOrWhiteSpace(command.Email) ? null : await userManager.FindByEmailAsync(command.Email);
            var token = RecuperacaoDeSenha.DecodificarToken(command.Token);

            if (usuario is null || token is null || EncerradorDeAcesso.ContaEncerrada(usuario))
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(LinkInvalido));

            var resultado = await userManager.ResetPasswordAsync(usuario, token, command.NovaSenha);

            if (!resultado.Succeeded)
            {
                // Erro de política de senha é do usuário e pode ser mostrado; o resto (token) responde o texto único.
                var deSenha = resultado.Errors.Where(e => e.Code.StartsWith("Password", StringComparison.Ordinal)).Select(e => e.Description).ToList();

                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(deSenha.Count > 0 ? deSenha : [LinkInvalido]));
            }

            // Quem prova que lê o e-mail da conta destrava o bloqueio por tentativas e perde todas as sessões antigas.
            await userManager.ResetAccessFailedCountAsync(usuario);
            await userManager.SetLockoutEndDateAsync(usuario, null);

            usuario.AccessTokenVersionId = Guid.NewGuid();
            await userManager.UpdateAsync(usuario);
            await refreshTokenProvider.RevogarTokensUsuarioAsync(usuario.Id, "Senha redefinida por e-mail");

            logger.LogInformation("Senha redefinida por e-mail para o usuário {UserId}.", usuario.Id);

            if (auditoria is not null)
                await auditoria.RegistrarAsync(EventosDeAutenticacao.SenhaRedefinida, usuario, cancellationToken);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao redefinir a senha por e-mail.");

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
