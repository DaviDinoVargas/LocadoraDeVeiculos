using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using System.Security.Claims;

namespace LocadoraDeVeiculos.WebApi.Identify;

public class ApiTenantProvider(IHttpContextAccessor contextAccessor) : ITenantProvider
{
    public Guid? UsuarioId
    {
        get
        {
            var claimId = contextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

            if (claimId == null)
                return null;

            return Guid.Parse(claimId.Value);
        }
    }
}