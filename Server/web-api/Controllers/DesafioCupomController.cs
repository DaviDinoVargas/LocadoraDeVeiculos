using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloDesafioCupom;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/desafios-cupom")]
    public sealed class DesafioCupomController(IMediator mediator) : MainController
    {
        [HttpPost]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<CadastrarDesafioCupomResponse>> Cadastrar(
            CadastrarDesafioCupomRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CadastrarDesafioCupomCommand(
                request.Nome, request.Descricao, request.MetaQuantidadeAlugueis, request.PeriodoDias, request.CupomRecompensaId);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarDesafioCupomResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<EditarDesafioCupomResponse>> Editar(
            Guid id,
            EditarDesafioCupomRequest request,
            CancellationToken cancellationToken)
        {
            var command = new EditarDesafioCupomCommand(
                id, request.Nome, request.Descricao, request.MetaQuantidadeAlugueis,
                request.PeriodoDias, request.CupomRecompensaId, request.Ativo);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarDesafioCupomResponse(valor.Nome, valor.Descricao, valor.MetaQuantidadeAlugueis, valor.PeriodoDias);
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<ExcluirDesafioCupomResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirDesafioCupomCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarDesafiosCupomResponse>> SelecionarTodos(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new SelecionarDesafiosCupomQuery(), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(d => new Models.ModuloDesafioCupom.SelecionarDesafiosCupomDto(
                        d.Id, d.Nome, d.Descricao, d.MetaQuantidadeAlugueis, d.PeriodoDias, d.CupomRecompensaCodigo, d.Ativo))
                    .ToList();

                return Ok(new SelecionarDesafiosCupomResponse(registros));
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarDesafioCupomPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new SelecionarDesafioCupomPorIdQuery(id), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarDesafioCupomPorIdResponse(
                    valor.Id, valor.Nome, valor.Descricao, valor.MetaQuantidadeAlugueis, valor.PeriodoDias,
                    valor.CupomRecompensaId, valor.CupomRecompensaCodigo, valor.Ativo);
                return Ok(response);
            });
        }

        [HttpGet("cliente/{clienteId:guid}/progresso")]
        public async Task<ActionResult<VerificarDesafiosClienteResponse>> VerificarProgressoCliente(
            Guid clienteId,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new VerificarDesafiosClienteQuery(clienteId), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Progresso
                    .Select(p => new Models.ModuloDesafioCupom.ProgressoDesafioDto(p.DesafioId, p.Nome, p.Meta, p.Progresso, p.Cumprido, p.CupomLiberadoCodigo))
                    .ToList();

                return Ok(new VerificarDesafiosClienteResponse(registros));
            });
        }
    }
}
