using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloAutomovel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/automoveis")]
    public sealed class AutomovelController(IMediator mediator) : MainController
    {
        [HttpPost]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<CadastrarAutomovelResponse>> Cadastrar(
            CadastrarAutomovelRequest request,
            CancellationToken cancellationToken
        )
        {
            var command = new CadastrarAutomovelCommand(
                request.Placa,
                request.Marca,
                request.Cor,
                request.Modelo,
                request.TipoCombustivel,
                request.CapacidadeTanque,
                request.Ano,
                request.Foto,
                request.GrupoAutomovelId
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarAutomovelResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<EditarAutomovelResponse>> Editar(
           Guid id,
           EditarAutomovelRequest request,
           CancellationToken cancellationToken
        )
        {
            var command = new EditarAutomovelCommand(
                id,
                request.Placa,
                request.Marca,
                request.Cor,
                request.Modelo,
                request.TipoCombustivel,
                request.CapacidadeTanque,
                request.Ano,
                request.Foto,
                request.GrupoAutomovelId
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarAutomovelResponse(
                    valor.Placa,
                    valor.Marca,
                    valor.Cor,
                    valor.Modelo,
                    valor.TipoCombustivel,
                    valor.CapacidadeTanque,
                    valor.Ano,
                    valor.Foto,
                    valor.GrupoAutomovelId
                );
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<ExcluirAutomovelResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            var command = new ExcluirAutomovelCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarAutomoveisResponse>> SelecionarTodos(
            CancellationToken cancellationToken
        )
        {
            var query = new SelecionarAutomoveisQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloAutomovel.SelecionarAutomoveisDto(
                        r.Id,
                        r.Placa,
                        r.Marca,
                        r.Cor,
                        r.Modelo,
                        r.TipoCombustivel,
                        r.CapacidadeTanque,
                        r.Ano,
                        r.Foto,
                        r.GrupoAutomovelId
                    ))
                    .ToList()
                    .AsReadOnly();

                var response = new SelecionarAutomoveisResponse(registros);
                return Ok(response);
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarAutomovelPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken
        )
        {
            var query = new SelecionarAutomovelPorIdQuery(id);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarAutomovelPorIdResponse(
                    valor.Id,
                    valor.Placa,
                    valor.Marca,
                    valor.Cor,
                    valor.Modelo,
                    valor.TipoCombustivel,
                    valor.CapacidadeTanque,
                    valor.Ano,
                    valor.Foto,
                    valor.GrupoAutomovelId,
                    valor.GrupoAutomovelNome
                );
                return Ok(response);
            });
        }

        [HttpGet("grupo/{grupoId:guid}")]
        public async Task<ActionResult<SelecionarAutomoveisPorGrupoResponse>> SelecionarPorGrupo(
            Guid grupoId,
            CancellationToken cancellationToken
        )
        {
            var query = new SelecionarAutomoveisPorGrupoQuery(grupoId);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloAutomovel.SelecionarAutomoveisDto(
                        r.Id,
                        r.Placa,
                        r.Marca,
                        r.Cor,
                        r.Modelo,
                        r.TipoCombustivel,
                        r.CapacidadeTanque,
                        r.Ano,
                        r.Foto,
                        r.GrupoAutomovelId
                    ))
                    .ToList()
                    .AsReadOnly();

                var response = new SelecionarAutomoveisPorGrupoResponse(registros);

                return Ok(response);
            });
        }
    }
}
