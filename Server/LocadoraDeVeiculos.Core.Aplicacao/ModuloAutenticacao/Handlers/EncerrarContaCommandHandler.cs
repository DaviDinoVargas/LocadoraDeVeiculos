using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;

/// <summary>
/// Encerramento da conta da empresa (S1): a conta e as de todos os seus funcionários perdem o acesso para sempre e a
/// biometria e o consentimento de todos são apagados. Os dados de negócio (aluguéis, clientes...) não são tratados
/// aqui: seguem as regras de exclusão lógica de cada módulo.
/// </summary>
public class EncerrarContaCommandHandler(
    UserManager<Usuario> userManager,
    RepositorioFuncionarioEmOrm repositorioFuncionario,
    IEncerradorDeAcesso encerradorDeAcesso,
    ILogger<EncerrarContaCommandHandler> logger,
    IAuditoriaDeAutenticacao? auditoria = null
) : IRequestHandler<EncerrarContaCommand, Result>
{
    private const string SenhaIncorreta = "A senha está incorreta.";

    public async Task<Result> Handle(EncerrarContaCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var usuario = await userManager.FindByIdAsync(command.UsuarioId.ToString());

            if (usuario is null)
                return Result.Fail(ResultadosErro.NaoAutorizadoErro("Sessão inválida."));

            if (await userManager.IsLockedOutAsync(usuario))
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(SenhaIncorreta));

            if (!await userManager.CheckPasswordAsync(usuario, command.Senha ?? string.Empty))
            {
                await userManager.AccessFailedAsync(usuario);

                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(SenhaIncorreta));
            }

            // O filtro por empresa já limita a consulta aos funcionários desta empresa.
            var funcionarios = await repositorioFuncionario.SelecionarTodosAsync();

            foreach (var funcionario in funcionarios)
            {
                var contaDoFuncionario = await userManager.FindByIdAsync(funcionario.UsuarioId.ToString());

                if (contaDoFuncionario is not null)
                    await encerradorDeAcesso.EncerrarAsync(contaDoFuncionario, "Conta da empresa encerrada", cancellationToken);
            }

            if (auditoria is not null)
                await auditoria.RegistrarAsync(EventosDeAutenticacao.ContaEncerrada, usuario, cancellationToken);

            await encerradorDeAcesso.EncerrarAsync(usuario, "Conta encerrada", cancellationToken);

            logger.LogInformation("Conta encerrada: usuário {UserId} e {Funcionarios} funcionário(s).", usuario.Id, funcionarios.Count());

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao encerrar a conta do usuário {UserId}.", command.UsuarioId);

            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
