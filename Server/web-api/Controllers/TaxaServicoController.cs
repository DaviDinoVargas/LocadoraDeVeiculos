using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloTaxaServico;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/taxas-servicos")]
    public sealed class TaxaServicoController(IMediator mediator) : MainController
    {
        [HttpPost]
        public async Task<ActionResult<CadastrarTaxaServicoResponse>> Cadastrar(
            CadastrarTaxaServicoRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<TipoCalculo>(request.TipoCalculo, out var tipoCalculo))
                return BadRequest("Tipo de cálculo inválido.");

            var command = new CadastrarTaxaServicoCommand(
                request.Nome,
                request.Preco,
                tipoCalculo
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarTaxaServicoResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<EditarTaxaServicoResponse>> Editar(
           Guid id,
           EditarTaxaServicoRequest request,
           CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<TipoCalculo>(request.TipoCalculo, out var tipoCalculo))
                return BadRequest("Tipo de cálculo inválido.");

            var command = new EditarTaxaServicoCommand(
                id,
                request.Nome,
                request.Preco,
                tipoCalculo
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarTaxaServicoResponse(
                    valor.Nome,
                    valor.Preco,
                    valor.TipoCalculo.ToString()
                );
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ExcluirTaxaServicoResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirTaxaServicoCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarTaxasServicoResponse>> SelecionarTodos(
    CancellationToken cancellationToken)
        {
            var query = new SelecionarTaxasServicoQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloTaxaServico.SelecionarTaxasServicoDto(
                        r.Id,
                        r.Nome,
                        r.Preco,
                        r.TipoCalculo.ToString()
                    ))
                    .ToList();

                var response = new SelecionarTaxasServicoResponse(registros);
                return Ok(response);
            });
        }


        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarTaxaServicoPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var query = new SelecionarTaxaServicoPorIdQuery(id);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarTaxaServicoPorIdResponse(
                    valor.Id,
                    valor.Nome,
                    valor.Preco,
                    valor.TipoCalculo.ToString()
                );
                return Ok(response);
            });
        }

        [HttpGet("tipo/{tipoCalculo}")]
        public async Task<ActionResult<SelecionarTaxasServicoPorTipoResponse>> SelecionarPorTipo(
     string tipoCalculo,
     CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<TipoCalculo>(tipoCalculo, out var tipo))
                return BadRequest("Tipo de cálculo inválido.");

            var query = new SelecionarTaxasServicoPorTipoQuery(tipo);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloTaxaServico.SelecionarTaxasServicoDto(
                        r.Id,
                        r.Nome,
                        r.Preco,
                        r.TipoCalculo.ToString()
                    ))
                    .ToList();

                var response = new SelecionarTaxasServicoPorTipoResponse(registros);
                return Ok(response);
            });
        }

    }
}