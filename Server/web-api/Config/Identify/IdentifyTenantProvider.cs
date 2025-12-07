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

    public bool EstaNoCargo(string cargo)
    {
        return contextAccessor.HttpContext?.User?.IsInRole(cargo) ?? false;
    }
}