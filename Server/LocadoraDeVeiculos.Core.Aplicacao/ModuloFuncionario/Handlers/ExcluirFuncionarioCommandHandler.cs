using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Handlers;

public class ExcluirFuncionarioCommandHandler(
    LocadoraDeVeiculosDbContext dbContext,
    RepositorioFuncionarioEmOrm repositorioFuncionario,
    RefreshTokenProvider refreshTokenProvider,
    UserManager<Usuario> userManager,
    IFacialAuthClient facialAuthClient,
    ILogger<ExcluirFuncionarioCommandHandler> logger
) : IRequestHandler<ExcluirFuncionarioCommand, Result<ExcluirFuncionarioResult>>
{
    /// <summary>
    /// Desligar alguém precisa cortar o acesso na hora, não em até 15 minutos: derruba a sessão (refresh tokens e a versão do
    /// access token), bloqueia a conta (sem isso o ex-funcionário ainda poderia entrar por senha ou rosto e o e-mail
    /// continuaria preso) e apaga a biometria, que não tem mais finalidade.
    /// </summary>
    private async Task EncerrarAcessoAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        await refreshTokenProvider.RevogarTokensUsuarioAsync(usuario.Id, "Exclusão");

        usuario.AccessTokenVersionId = Guid.NewGuid();
        await userManager.UpdateAsync(usuario);

        await userManager.SetLockoutEnabledAsync(usuario, true);
        await userManager.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);

        if (!string.IsNullOrWhiteSpace(usuario.Email))
            await facialAuthClient.RemoverAsync(FacialAuthClient.PersonIdParaEmail(usuario.Email), cancellationToken);
    }

    public async Task<Result<ExcluirFuncionarioResult>> Handle(
        ExcluirFuncionarioCommand command, CancellationToken cancellationToken)
    {
        try
        {
            Funcionario? funcionario = await repositorioFuncionario.SelecionarPorIdAsync(command.Id);

            if (funcionario is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            Usuario? usuario = await userManager.FindByIdAsync(funcionario.UsuarioId.ToString());

            if (usuario is not null)
                await EncerrarAcessoAsync(usuario, cancellationToken);

            await repositorioFuncionario.ExcluirAsync(funcionario.Id);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok(new ExcluirFuncionarioResult());
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Ocorreu um erro durante a exclusão de de {Comando}.",
                command.GetType().Name);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}