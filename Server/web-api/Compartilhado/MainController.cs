using FluentResults;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Compartilhado;

[ApiController]
public abstract class MainController : ControllerBase
{
    protected ActionResult ProcessarResultado<T>(Result<T> result, Func<T, ActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess is not null ? onSuccess(result.Value) : Ok(result.Value);

        return MapearErro(result.Errors);
    }

    protected ActionResult ProcessarResultado(Result result, Func<ActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
            return onSuccess is not null ? onSuccess() : NoContent();

        return MapearErro(result.Errors);
    }

    private ActionResult MapearErro(IReadOnlyList<IError> errors)
    {
        var erroPrincipal = errors.FirstOrDefault();

        var detalhes = errors.Select(e => e.Message).ToList();

        // Extrai causas de erro aninhadas
        foreach (var error in errors)
            detalhes.AddRange(error.Reasons.Select(r => r.Message));

        if (erroPrincipal == null)
            return BadRequest(detalhes);

        if (erroPrincipal.HasMetadataKey("TipoErro"))
        {
            var tipoErro = erroPrincipal.Metadata["TipoErro"] as string;

            return tipoErro switch
            {
                "RequisicaoInvalida" => BadRequest(detalhes),           // 400
                "RegistroNaoEncontrado" => NotFound(detalhes),          // 404
                "RegistroDuplicado" => Conflict(detalhes),              // 409
                "ExclusaoBloqueada" => UnprocessableEntity(detalhes),   // 422
                "ExcecaoInterna" => StatusCode(500, detalhes),          // 500
                _ => BadRequest(detalhes)
            };
        }

        return BadRequest(detalhes);
    }
}
