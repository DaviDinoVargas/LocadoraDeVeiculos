using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;

namespace LocadoraDeVeiculos.WebApi.Config.Identify;

public class IdentityTenantProvider(IHttpContextAccessor contextAccessor) : ITenantProvider
{
    public Guid? EmpresaId
    {
        get
        {
            var user = contextAccessor.HttpContext?.User;

            var empresaIdClaim = user?.FindFirst("EmpresaId")?.Value;

            if (Guid.TryParse(empresaIdClaim, out var empresaId))
                return empresaId;

            return null;
        }
    }

    public Guid? UsuarioId
    {
        get
        {
            var user = contextAccessor.HttpContext?.User;

            var id = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? user?.FindFirst("sub")?.Value;

            return Guid.TryParse(id, out var usuarioId) ? usuarioId : null;
        }
    }

    public string? EnderecoIp => contextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool EstaNoCargo(string cargo)
    {
        return contextAccessor.HttpContext?.User?.IsInRole(cargo) ?? false;
    }
}