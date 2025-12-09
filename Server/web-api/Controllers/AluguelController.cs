using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloAluguel;
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
    [Route("api/alugueis")]
    public sealed class AluguelController(IMediator mediator) : MainController
    {
        [HttpPost]
        public async Task<ActionResult<CadastrarAluguelResponse>> Cadastrar(
            CadastrarAluguelRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CadastrarAluguelCommand(
                request.CondutorId,
                request.AutomovelId,
                request.ClienteId,
                request.DataSaida,
                request.DataRetornoPrevisto,
                request.ValorPrevisto,
                request.TaxasServicosIds
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarAluguelResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<EditarAluguelResponse>> Editar(
           Guid id,
           EditarAluguelRequest request,
           CancellationToken cancellationToken)
        {
            var command = new EditarAluguelCommand(
                id,
                request.CondutorId,
                request.AutomovelId,
                request.ClienteId,
                request.DataSaida,
                request.DataRetornoPrevisto,
                request.ValorPrevisto,
                request.TaxasServicosIds
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarAluguelResponse(
                    valor.Id,
                    valor.CondutorId,
                    valor.AutomovelId,
                    valor.ClienteId,
                    valor.DataSaida,
                    valor.DataRetornoPrevisto,
                    valor.ValorPrevisto
                );
                return Ok(response);
            });
        }

        [HttpPost("{id:guid}/iniciar")]
        public async Task<ActionResult<IniciarAluguelResponse>> Iniciar(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new IniciarAluguelCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new IniciarAluguelResponse(valor.Id, valor.Status.ToString());
                return Ok(response);
            });
        }

        [HttpPost("{id:guid}/cancelar")]
        public async Task<ActionResult<CancelarAluguelResponse>> Cancelar(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new CancelarAluguelCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CancelarAluguelResponse(valor.Id, valor.Status.ToString());
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ExcluirAluguelResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirAluguelCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarAlugueisResponse>> SelecionarTodos(
            CancellationToken cancellationToken)
        {
            var query = new SelecionarAlugueisQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloAluguel.SelecionarAlugueisDto(
                        r.Id,
                        r.CondutorNome,
                        r.AutomovelPlaca,
                        r.ClienteNome,
                        r.DataSaida,
                        r.DataRetornoPrevisto,
                        r.ValorPrevisto,
                        r.Status
                    ))
                    .ToList();

                var response = new SelecionarAlugueisResponse(registros);

                return Ok(response);
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarAluguelPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var query = new SelecionarAluguelPorIdQuery(id);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var taxas = valor.TaxasServicos
                    .Select(t => new Models.ModuloAluguel.SelecionarTaxaServicoDto(
                        t.Id,
                        t.Nome,
                        t.Preco,
                        t.TipoCalculo.ToString()
                    ))
                    .ToList();

                var response = new SelecionarAluguelPorIdResponse(
                    valor.Id,
                    valor.CondutorId,
                    valor.CondutorNome,
                    valor.AutomovelId,
                    valor.AutomovelPlaca,
                    valor.ClienteId,
                    valor.ClienteNome,
                    valor.DataSaida,
                    valor.DataRetornoPrevisto,
                    valor.ValorPrevisto,
                    valor.ValorCaucao,
                    valor.Status,
                    taxas
                );

                return Ok(response);
            });
        }

        [HttpGet("status/{status}")]
        public async Task<ActionResult<SelecionarAlugueisPorStatusResponse>> SelecionarPorStatus(
            string status,
            CancellationToken cancellationToken)
        {
            var query = new SelecionarAlugueisPorStatusQuery(status);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloAluguel.SelecionarAlugueisDto(
                        r.Id,
                        r.CondutorNome,
                        r.AutomovelPlaca,
                        r.ClienteNome,
                        r.DataSaida,
                        r.DataRetornoPrevisto,
                        r.ValorPrevisto,
                        r.Status
                    ))
                    .ToList();

                var response = new SelecionarAlugueisPorStatusResponse(registros);

                return Ok(response);
            });
        }

        [HttpGet("em-aberto")]
        public async Task<ActionResult<SelecionarAlugueisEmAbertoResponse>> SelecionarEmAberto(
            CancellationToken cancellationToken)
        {
            var query = new SelecionarAlugueisEmAbertoQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloAluguel.SelecionarAlugueisDto(
                        r.Id,
                        r.CondutorNome,
                        r.AutomovelPlaca,
                        r.ClienteNome,
                        r.DataSaida,
                        r.DataRetornoPrevisto,
                        r.ValorPrevisto,
                        r.Status
                    ))
                    .ToList();

                var response = new SelecionarAlugueisEmAbertoResponse(registros);

                return Ok(response);
            });
        }
    }
}
