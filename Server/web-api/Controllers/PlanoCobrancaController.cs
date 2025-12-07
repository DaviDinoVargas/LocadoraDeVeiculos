using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloPlanoCobranca;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa")]
    [Route("api/planos-cobranca")]
    public class PlanoCobrancaController : MainController
    {
        private readonly IMediator mediator;

        public PlanoCobrancaController(IMediator mediator)
            => this.mediator = mediator;

        [HttpPost]
        public async Task<ActionResult> Cadastrar(
            CadastrarPlanoCobrancaRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CadastrarPlanoCobrancaCommand(
                request.GrupoAutomovelId,
                request.Nome,
                request.PrecoDiaria,
                request.PrecoPorKm,
                request.KmLivreLimite
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, valor =>
            {
                var response = new CadastrarPlanoCobrancaResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult> Editar(
            Guid id,
            EditarPlanoCobrancaRequest request,
            CancellationToken cancellationToken)
        {
            var command = new EditarPlanoCobrancaCommand(
                id,
                request.GrupoAutomovelId,
                request.Nome,
                request.PrecoDiaria,
                request.PrecoPorKm,
                request.KmLivreLimite
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, valor =>
            {
                var response = new EditarPlanoCobrancaResponse(
                    valor.Nome,
                    valor.PrecoDiaria,
                    valor.PrecoPorKm,
                    valor.KmLivreLimite
                );

                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> Excluir(Guid id, CancellationToken cancellationToken)
        {
            var command = new ExcluirPlanoCobrancaCommand(id);
            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, _ => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult> SelecionarTodos(CancellationToken cancellationToken)
        {
            var query = new SelecionarPlanosCobrancaQuery();
            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, valor =>
            {
                var resp = new SelecionarPlanosCobrancaResponse(valor.Registros);
                return Ok(resp);
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult> SelecionarPorId(Guid id, CancellationToken cancellationToken)
        {
            var query = new SelecionarPlanoCobrancaPorIdQuery(id);
            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, valor =>
            {
                var response = new SelecionarPlanoCobrancaPorIdResponse(
                    valor.Id, valor.GrupoAutomovelId, valor.Nome,
                    valor.PrecoDiaria, valor.PrecoPorKm, valor.KmLivreLimite
                );
                return Ok(response);
            });
        }
    }

}
