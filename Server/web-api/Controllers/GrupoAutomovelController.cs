using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloGrupoAutomovel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa")]
    [Route("api/grupos-automovel")]
    public sealed class GrupoAutomovelController(IMediator mediator) : MainController
    {
        [HttpPost]
        public async Task<ActionResult<CadastrarGrupoAutomovelResponse>> Cadastrar(
            CadastrarGrupoAutomovelRequest request,
            CancellationToken cancellationToken
        )
        {
            var command = new CadastrarGrupoAutomovelCommand(
                request.Nome,
                request.Descricao
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarGrupoAutomovelResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<EditarGrupoAutomovelResponse>> Editar(
           Guid id,
           EditarGrupoAutomovelRequest request,
           CancellationToken cancellationToken
        )
        {
            var command = new EditarGrupoAutomovelCommand(
                id,
                request.Nome,
                request.Descricao
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarGrupoAutomovelResponse(
                    valor.Nome,
                    valor.Descricao
                );
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ExcluirGrupoAutomovelResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            var command = new ExcluirGrupoAutomovelCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarGruposAutomovelResponse>> SelecionarTodos(
            CancellationToken cancellationToken
        )
        {
            var query = new SelecionarGruposAutomovelQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registrosWebApi = valor.Registros
                    .Select(r => new LocadoraDeVeiculos.WebApi.Models.ModuloGrupoAutomovel.SelecionarGruposAutomovelDto(
                        r.Id,
                        r.Nome,
                        r.Descricao
                    ))
                    .ToList();

                var response = new SelecionarGruposAutomovelResponse(registrosWebApi);
                return Ok(response);
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarGrupoAutomovelPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            var query = new SelecionarGrupoAutomovelPorIdQuery(id);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarGrupoAutomovelPorIdResponse(
                    valor.Id,
                    valor.Nome,
                    valor.Descricao
                );
                return Ok(response);
            });
        }
    }
}