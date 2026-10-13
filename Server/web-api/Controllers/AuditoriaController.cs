using LocadoraDeVeiculos.Core.Aplicacao.ModuloAuditoria;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Controllers;

/// <summary>Trilha de auditoria da própria empresa (S14). Só o papel Empresa enxerga.</summary>
[Authorize(Roles = "Empresa")]
[Route("api/auditoria")]
public sealed class AuditoriaController(IMediator mediator) : MainController
{
    [HttpGet]
    public async Task<ActionResult<ListarAuditoriaResult>> Listar(
        [FromQuery] int limite = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new ListarAuditoriaQuery(limite), cancellationToken);

        return ProcessarResultado(result, valor => Ok(new { registros = valor.Registros }));
    }
}
