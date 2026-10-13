using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LocadoraDeVeiculos.WebApi.Filters;

/// <summary>
/// Defesa em profundidade contra CSRF nos endpoints que agem só com o cookie (rotacionar e sair).
/// O cookie já é SameSite=Lax, mas se front e API passarem a domínios diferentes e o cookie virar
/// SameSite=None, nada impediria um site de terceiros de disparar essas chamadas. Exigir um cabeçalho
/// personalizado resolve: o navegador só o envia entre origens depois de um preflight de CORS, e a
/// API só autoriza o preflight das origens listadas em CORS_ALLOWED_ORIGINS. Formulários e imagens
/// de outros sites não conseguem incluir cabeçalhos personalizados.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExigirCabecalhoClienteAttribute : ActionFilterAttribute
{
    public const string Cabecalho = "X-Locadora-Client";
    public const string Valor = "web";

    public override void OnActionExecuting(ActionExecutingContext contexto)
    {
        if (contexto.HttpContext.Request.Headers[Cabecalho] != Valor)
        {
            contexto.Result = new ObjectResult(new[] { "Requisição sem o cabeçalho do cliente." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
