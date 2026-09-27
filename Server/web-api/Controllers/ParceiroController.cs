using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloParceiro;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa")]
    [Route("api/parceiros")]
    public sealed class ParceiroController(IMediator mediator) : MainController
    {
        [HttpPost]
        public async Task<ActionResult<CadastrarParceiroResponse>> Cadastrar(
            CadastrarParceiroRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<CategoriaParceiro>(request.Categoria, out var categoria))
                return BadRequest("Categoria de parceiro inválida.");

            var command = new CadastrarParceiroCommand(request.Nome, request.Cnpj, categoria);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarParceiroResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<EditarParceiroResponse>> Editar(
            Guid id,
            EditarParceiroRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<CategoriaParceiro>(request.Categoria, out var categoria))
                return BadRequest("Categoria de parceiro inválida.");

            var command = new EditarParceiroCommand(id, request.Nome, request.Cnpj, categoria, request.Ativo);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarParceiroResponse(valor.Nome, valor.Cnpj, valor.Categoria.ToString(), valor.Ativo);
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ExcluirParceiroResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirParceiroCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        [Authorize(Roles = "Empresa,Funcionario")]
        public async Task<ActionResult<SelecionarParceirosResponse>> SelecionarTodos(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new SelecionarParceirosQuery(), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(p => new Models.ModuloParceiro.SelecionarParceirosDto(p.Id, p.Nome, p.Cnpj, p.Categoria.ToString(), p.Ativo))
                    .ToList();

                return Ok(new SelecionarParceirosResponse(registros));
            });
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Empresa,Funcionario")]
        public async Task<ActionResult<SelecionarParceiroPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new SelecionarParceiroPorIdQuery(id), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarParceiroPorIdResponse(valor.Id, valor.Nome, valor.Cnpj, valor.Categoria.ToString(), valor.Ativo);
                return Ok(response);
            });
        }
    }
}
