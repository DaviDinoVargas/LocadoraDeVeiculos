using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloCupom;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/cupons")]
    public sealed class CupomController(IMediator mediator) : MainController
    {
        [HttpPost]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<CadastrarCupomResponse>> Cadastrar(
            CadastrarCupomRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<TipoDesconto>(request.TipoDesconto, out var tipoDesconto))
                return BadRequest("Tipo de desconto inválido.");

            var command = new CadastrarCupomCommand(
                request.Codigo, request.Descricao, tipoDesconto, request.ValorDesconto,
                request.ValidoAte, request.LimiteUsos, request.ParceiroId);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new CadastrarCupomResponse(valor.Id);
                return CreatedAtAction(nameof(SelecionarPorId), new { id = valor.Id }, response);
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<EditarCupomResponse>> Editar(
            Guid id,
            EditarCupomRequest request,
            CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<TipoDesconto>(request.TipoDesconto, out var tipoDesconto))
                return BadRequest("Tipo de desconto inválido.");

            var command = new EditarCupomCommand(
                id, request.Codigo, request.Descricao, tipoDesconto, request.ValorDesconto,
                request.ValidoAte, request.LimiteUsos, request.ParceiroId, request.Ativo);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new EditarCupomResponse(valor.Codigo, valor.Descricao, valor.ValorDesconto, valor.ValidoAte);
                return Ok(response);
            });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Empresa")]
        public async Task<ActionResult<ExcluirCupomResponse>> Excluir(
            Guid id,
            CancellationToken cancellationToken)
        {
            var command = new ExcluirCupomCommand(id);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (_) => NoContent());
        }

        [HttpGet]
        public async Task<ActionResult<SelecionarCuponsResponse>> SelecionarTodos(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new SelecionarCuponsQuery(), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var registros = valor.Registros
                    .Select(c => new Models.ModuloCupom.SelecionarCuponsDto(
                        c.Id, c.Codigo, c.Descricao, c.TipoDesconto.ToString(), c.ValorDesconto,
                        c.ValidoAte, c.LimiteUsos, c.UsosAtuais, c.Ativo))
                    .ToList();

                return Ok(new SelecionarCuponsResponse(registros));
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SelecionarCupomPorIdResponse>> SelecionarPorId(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new SelecionarCupomPorIdQuery(id), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new SelecionarCupomPorIdResponse(
                    valor.Id, valor.Codigo, valor.Descricao, valor.TipoDesconto.ToString(), valor.ValorDesconto,
                    valor.ValidoAte, valor.LimiteUsos, valor.UsosAtuais, valor.ParceiroId, valor.ParceiroNome, valor.Ativo);
                return Ok(response);
            });
        }

        [HttpPost("validar")]
        public async Task<ActionResult<ValidarCupomResponse>> Validar(
            ValidarCupomRequest request,
            CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new ValidarCupomQuery(request.Codigo, request.ValorBase), cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new ValidarCupomResponse(valor.Valido, valor.MotivoInvalido, valor.ValorDesconto, valor.ValorComDesconto);
                return Ok(response);
            });
        }
    }
}
