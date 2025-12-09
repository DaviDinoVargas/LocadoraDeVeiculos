using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloConfiguracao;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa")]
    [Route("api/configuracoes")]
    public sealed class ConfiguracaoController(IMediator mediator) : MainController
    {
        [HttpPut("preco-combustivel")]
        public async Task<ActionResult<ConfigurarPrecoCombustivelResponse>> ConfigurarPrecoCombustivel(
            ConfigurarPrecoCombustivelRequest request,
            CancellationToken cancellationToken)
        {
            var command = new ConfigurarPrecoCombustivelCommand(request.PrecoCombustivel);

            var result = await mediator.Send(command, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new ConfigurarPrecoCombustivelResponse(
                    valor.Id,
                    valor.PrecoCombustivel,
                    valor.ValorGarantia
                );
                return Ok(response);
            });
        }

        [HttpGet]
        public async Task<ActionResult<ObterConfiguracaoResponse>> ObterConfiguracao(
            CancellationToken cancellationToken)
        {
            var query = new ObterConfiguracaoQuery();

            var result = await mediator.Send(query, cancellationToken);

            return ProcessarResultado(result, (valor) =>
            {
                var response = new ObterConfiguracaoResponse(
                    valor.Id,
                    valor.PrecoCombustivel,
                    valor.ValorGarantia
                );
                return Ok(response);
            });
        }
    }
}