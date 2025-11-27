using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Filters;

public class ResponseWrapperFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is JsonResult jsonResult)
        {
            var valor = jsonResult.Value;

            if (valor is IEnumerable<string> mensagensDeErro)
            {
                jsonResult.Value = new
                {
                    Sucesso = false,
                    Erros = mensagensDeErro
                };
            }
            else
            {
                jsonResult.Value = new
                {
                    Sucesso = true,
                    Dados = valor
                };
            }
        }
    }
}