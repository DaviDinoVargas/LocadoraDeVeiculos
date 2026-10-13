using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

/// <summary>Eventos de autenticação que entram na trilha de auditoria da empresa (S14).</summary>
public static class EventosDeAutenticacao
{
    public const string Entidade = "Autenticacao";
    public const string LoginOk = "LoginOk";
    public const string LoginFalhou = "LoginFalhou";
    public const string LoginBloqueado = "LoginBloqueado";
    public const string LoginFacialOk = "LoginFacialOk";
    public const string LoginFacialFalhou = "LoginFacialFalhou";
    public const string LoginFacialBloqueado = "LoginFacialBloqueado";
    public const string SenhaAlterada = "SenhaAlterada";
    public const string ContaEncerrada = "ContaEncerrada";
    public const string SenhaRedefinida = "SenhaRedefinida";
    public const string DoisFatoresAtivado = "DoisFatoresAtivado";
    public const string DoisFatoresDesativado = "DoisFatoresDesativado";
    public const string ReusoDeRefreshToken = "ReusoDeRefreshToken";
    public const string Logout = "Logout";
    public const string RostoCadastrado = "RostoCadastrado";
    public const string RostoRemovido = "RostoRemovido";
    public const string RostoVerificado = "RostoVerificado";
    public const string RostoNaoConfere = "RostoNaoConfere";
}

public interface IAuditoriaDeAutenticacao
{
    /// <summary>Registra o evento na trilha da empresa do usuário. Nunca lança: auditar não pode derrubar o login.</summary>
    Task RegistrarAsync(string evento, Usuario usuario, CancellationToken cancellationToken = default);
}

public class AuditoriaDeAutenticacao(
    LocadoraDeVeiculosDbContext dbContext,
    UserManager<Usuario> userManager,
    ITenantProvider tenant,
    ILogger<AuditoriaDeAutenticacao> logger
) : IAuditoriaDeAutenticacao
{
    public async Task RegistrarAsync(string evento, Usuario usuario, CancellationToken cancellationToken = default)
    {
        try
        {
            var empresaId = await ResolverEmpresaAsync(usuario, cancellationToken);

            // Sem empresa (usuário sem cargo) não há trilha a que o registro pertença: o filtro por empresa o esconderia.
            if (empresaId is null)
                return;

            dbContext.Set<RegistroDeAuditoria>().Add(new RegistroDeAuditoria
            {
                EmpresaId = empresaId,
                UsuarioId = usuario.Id,
                Acao = evento,
                Entidade = EventosDeAutenticacao.Entidade,
                EnderecoIp = tenant.EnderecoIp
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível registrar o evento de autenticação {Evento}.", evento);
        }
    }

    // Empresa: o tenant é o próprio usuário. Funcionário: a empresa vinculada (a consulta ignora o filtro por
    // tenant porque, no login, ainda não existe tenant no contexto: é isto que se está descobrindo).
    private async Task<Guid?> ResolverEmpresaAsync(Usuario usuario, CancellationToken cancellationToken)
    {
        var cargos = await userManager.GetRolesAsync(usuario);

        if (cargos.Contains(CargoUsuario.Empresa.ToString()))
            return usuario.Id;

        if (cargos.Contains(CargoUsuario.Funcionario.ToString()))
            return await dbContext.Set<Funcionario>().AsNoTracking().IgnoreQueryFilters()
                .Where(f => f.UsuarioId == usuario.Id)
                .Select(f => (Guid?)f.EmpresaId)
                .FirstOrDefaultAsync(cancellationToken);

        return null;
    }
}
