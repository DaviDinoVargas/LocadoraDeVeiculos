using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloCondutor;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/condutores")]
    public sealed class CondutorController(IMediator mediator) : MainController
    {
        [HttpPost]
        public async Task<ActionResult<CadastrarCondutorResponse>> Cadastrar(
            CadastrarCondutorRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CadastrarCondutorCommand(
                request.Nome,
                request.Email,
                request.Cpf,
                request.Cnh,
                request.ValidadeCnh,
                request.Telefone,
                request.ClienteId
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarCondutorResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<EditarCondutorResponse>> Editar(
           Guid id,
           EditarCondutorRequest request,
           CancellationToken cancellationToken)
        {
            var command = new EditarCondutorCommand(
                id,
                request.Nome,
                request.Email,
                request.Cpf,
                request.Cnh,
                request.ValidadeCnh,
                request.Telefone,
                request.ClienteId
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarCondutorResponse(
                    valor.Id,
                    valor.Nome,
                    valor.Email,
                    valor.Cpf,
                    valor.Cnh,
                    valor.ValidadeCnh,
                    valor.Telefone,
                    valor.ClienteId
                );
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ExcluirCondutorResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirCondutorCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarCondutoresResponse>> SelecionarTodos(
    CancellationToken cancellationToken)
        {
            var query = new SelecionarCondutoresQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloCondutor.SelecionarCondutoresDto(
                        r.Id,
                        r.Nome,
                        r.Email,
                        r.Cpf,
                        r.Cnh,
                        r.ValidadeCnh,
                        r.Telefone,
                        r.ClienteId,
                        r.ClienteNome
                    ))
                    .ToList()
                    .AsReadOnly();

                var response = new SelecionarCondutoresResponse(registros);

                return Ok(response);
            });
        }


        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarCondutorPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var query = new SelecionarCondutorPorIdQuery(id);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarCondutorPorIdResponse(
                    valor.Id,
                    valor.Nome,
                    valor.Email,
                    valor.Cpf,
                    valor.Cnh,
                    valor.ValidadeCnh,
                    valor.Telefone,
                    valor.ClienteId,
                    valor.ClienteNome
                );
                return Ok(response);
            });
        }

        [HttpGet("cliente/{clienteId:guid}")]
        public async Task<ActionResult<SelecionarCondutoresPorClienteResponse>> SelecionarPorCliente(
    Guid clienteId,
    CancellationToken cancellationToken)
        {
            var query = new SelecionarCondutoresPorClienteQuery(clienteId);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(r => new Models.ModuloCondutor.SelecionarCondutoresDto(
                        r.Id,
                        r.Nome,
                        r.Email,
                        r.Cpf,
                        r.Cnh,
                        r.ValidadeCnh,
                        r.Telefone,
                        r.ClienteId,
                        r.ClienteNome
                    ))
                    .ToList()
                    .AsReadOnly();

                var response = new SelecionarCondutoresPorClienteResponse(registros);

                return Ok(response);
            });
        }

    }
}