using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloDevolucao;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/devolucoes")]
    public sealed class DevolucaoController(IMediator mediator) : MainController
    {
        [HttpPost]
        public async Task<ActionResult<RegistrarDevolucaoResponse>> Registrar(
            RegistrarDevolucaoRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<NivelCombustivel>(request.NivelCombustivel, out var nivelCombustivel))
                return BadRequest("Nível de combustível inválido.");

            var command = new RegistrarDevolucaoCommand(
                request.AluguelId,
                request.DataDevolucao,
                request.QuilometragemFinal,
                request.CombustivelNoTanque,
                nivelCombustivel
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new RegistrarDevolucaoResponse(
                    valor.Id,
                    valor.AluguelId,
                    valor.DataDevolucao,
                    valor.QuilometragemFinal,
                    valor.CombustivelNoTanque,
                    valor.ValorMultas,
                    valor.ValorAdicionalCombustivel,
                    valor.ValorTotal
                );
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ExcluirDevolucaoResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirDevolucaoCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarDevolucoesResponse>> SelecionarTodos(
            CancellationToken cancellationToken)
        {
            var query = new SelecionarDevolucoesQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var dtos = valor.Registros.Select(x =>
                    new Models.ModuloDevolucao.SelecionarDevolucoesDto(
                        x.Id,
                        x.CondutorNome,
                        x.AutomovelPlaca,
                        x.ClienteNome,
                        x.DataDevolucao,
                        x.QuilometragemFinal,
                        x.ValorTotal
                    )
                ).ToList();

                var response = new SelecionarDevolucoesResponse(dtos);
                return Ok(response);
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarDevolucaoPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var query = new SelecionarDevolucaoPorIdQuery(id);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarDevolucaoPorIdResponse(
                    valor.Id,
                    valor.AluguelId,
                    valor.CondutorNome,
                    valor.AutomovelPlaca,
                    valor.ClienteNome,
                    valor.DataDevolucao,
                    valor.QuilometragemFinal,
                    valor.CombustivelNoTanque,
                    valor.NivelCombustivel,
                    valor.ValorMultas,
                    valor.ValorAdicionalCombustivel,
                    valor.ValorTotal
                );
                return Ok(response);
            });
        }

        [HttpGet("aluguel/{aluguelId:guid}")]
        public async Task<ActionResult<SelecionarDevolucaoPorAluguelIdResponse>> SelecionarPorAluguelId(
            Guid aluguelId,
            CancellationToken cancellationToken)
        {
            var query = new SelecionarDevolucaoPorAluguelIdQuery(aluguelId);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarDevolucaoPorAluguelIdResponse(
                    valor.Id,
                    valor.AluguelId,
                    valor.DataDevolucao,
                    valor.QuilometragemFinal,
                    valor.CombustivelNoTanque,
                    valor.NivelCombustivel,
                    valor.ValorMultas,
                    valor.ValorAdicionalCombustivel,
                    valor.ValorTotal
                );
                return Ok(response);
            });
        }
    }
}