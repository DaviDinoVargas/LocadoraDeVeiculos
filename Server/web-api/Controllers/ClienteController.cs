using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloCliente;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/clientes")]
    public sealed class ClienteController(IMediator mediator) : MainController
    {
        [HttpPost("pessoa-fisica")]
        public async Task<ActionResult<CadastrarClienteResponse>> CadastrarPessoaFisica(
            CadastrarClientePessoaFisicaRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CadastrarClientePessoaFisicaCommand(
                request.Nome,
                request.Cpf,
                request.Rg,
                request.Cnh,
                request.ValidadeCnh,
                request.Telefone,
                request.Email,
                request.Endereco,
                request.ClientePessoaJuridicaId
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarClienteResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPost("pessoa-juridica")]
        public async Task<ActionResult<CadastrarClienteResponse>> CadastrarPessoaJuridica(
            CadastrarClientePessoaJuridicaRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CadastrarClientePessoaJuridicaCommand(
                request.Nome,
                request.Cnpj,
                request.NomeFantasia,
                request.Telefone,
                request.Email,
                request.Endereco
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarClienteResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("pessoa-fisica/{id:guid}")]
        public async Task<ActionResult<EditarClienteResponse>> EditarPessoaFisica(
           Guid id,
           EditarClientePessoaFisicaRequest request,
           CancellationToken cancellationToken)
        {
            var command = new EditarClientePessoaFisicaCommand(
                id,
                request.Nome,
                request.Cpf,
                request.Rg,
                request.Cnh,
                request.ValidadeCnh,
                request.Telefone,
                request.Email,
                request.Endereco,
                request.ClientePessoaJuridicaId
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarClienteResponse(valor.Id, valor.Nome, valor.Documento, valor.TipoCliente);
                return Ok(response);
            });
        }

        [HttpPut("pessoa-juridica/{id:guid}")]
        public async Task<ActionResult<EditarClienteResponse>> EditarPessoaJuridica(
           Guid id,
           EditarClientePessoaJuridicaRequest request,
           CancellationToken cancellationToken)
        {
            var command = new EditarClientePessoaJuridicaCommand(
                id,
                request.Nome,
                request.Cnpj,
                request.NomeFantasia,
                request.Telefone,
                request.Email,
                request.Endereco
            );

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarClienteResponse(valor.Id, valor.Nome, valor.Documento, valor.TipoCliente);
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ExcluirClienteResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirClienteCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        [HttpGet]
        public async Task<ActionResult<SelecionarClientesResponse>> SelecionarTodos(
    CancellationToken cancellationToken)
        {
            var query = new SelecionarClientesQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros.Select(dto =>
                    new LocadoraDeVeiculos.WebApi.Models.ModuloCliente.SelecionarClientesDto(
                        dto.Id,
                        dto.Nome,
                        dto.Telefone,
                        dto.Email,
                        dto.TipoCliente,
                        dto.DocumentoPrincipal
                    )
                ).ToList();

                var response = new SelecionarClientesResponse(registros);

                return Ok(response);
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarClientePorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var query = new SelecionarClientePorIdQuery(id);

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarClientePorIdResponse(
                    valor.Id,
                    valor.Nome,
                    valor.Telefone,
                    valor.Email,
                    valor.Endereco,
                    valor.TipoCliente,
                    valor.Cpf,
                    valor.Rg,
                    valor.Cnh,
                    valor.ValidadeCnh,
                    valor.ClientePessoaJuridicaId,
                    valor.ClientePessoaJuridicaNome,
                    valor.Cnpj,
                    valor.NomeFantasia
                );
                return Ok(response);
            });
        }
        [HttpGet("pessoas-fisicas")]
        public async Task<ActionResult<SelecionarPessoasFisicasResponse>> SelecionarPessoasFisicas(
            CancellationToken cancellationToken)
        {
            var query = new SelecionarPessoasFisicasQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros.Select(dto =>
                    new LocadoraDeVeiculos.WebApi.Models.ModuloCliente.SelecionarPessoaFisicaDto(
                        dto.Id,
                        dto.Nome,
                        dto.Cpf,
                        dto.Rg,
                        dto.Cnh,
                        dto.ValidadeCnh,
                        dto.Telefone,
                        dto.Email,
                        dto.Endereco,
                        dto.ClientePessoaJuridicaId,
                        dto.ClientePessoaJuridicaNome
                    )
                ).ToList();

                var response = new SelecionarPessoasFisicasResponse(registros);

                return Ok(response);
            });
        }

        [HttpGet("pessoas-juridicas")]
        public async Task<ActionResult<SelecionarPessoasJuridicasResponse>> SelecionarPessoasJuridicas(
     CancellationToken cancellationToken)
        {
            var query = new SelecionarPessoasJuridicasQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros.Select(dto =>
                    new LocadoraDeVeiculos.WebApi.Models.ModuloCliente.SelecionarPessoaJuridicaDto(
                        dto.Id,
                        dto.Nome,
                        dto.Cnpj,
                        dto.NomeFantasia,
                        dto.Telefone,
                        dto.Email,
                        dto.Endereco
                    )
                ).ToList();

                var response = new SelecionarPessoasJuridicasResponse(registros);

                return Ok(response);
            });
        }
    }
}