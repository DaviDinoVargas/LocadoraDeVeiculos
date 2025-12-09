using FluentResults;
using MediatR;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands
{
    public record ConfigurarPrecoCombustivelCommand(
        decimal PrecoCombustivel
    ) : IRequest<Result<ConfigurarPrecoCombustivelResult>>;

    public record ConfigurarPrecoCombustivelResult(
        Guid Id,
        decimal PrecoCombustivel,
        decimal ValorGarantia
    );

    public record ObterConfiguracaoQuery() : IRequest<Result<ObterConfiguracaoResult>>;

    public record ObterConfiguracaoResult(
        Guid Id,
        decimal PrecoCombustivel,
        decimal ValorGarantia
    );
}