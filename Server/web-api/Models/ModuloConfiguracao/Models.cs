using System;

namespace LocadoraDeVeiculos.WebApi.Models.ModuloConfiguracao
{
    public record ConfigurarPrecoCombustivelRequest(
        decimal PrecoCombustivel
    );

    public record ConfigurarPrecoCombustivelResponse(
        Guid Id,
        decimal PrecoCombustivel,
        decimal ValorGarantia
    );

    public record ObterConfiguracaoResponse(
        Guid Id,
        decimal PrecoCombustivel,
        decimal ValorGarantia
    );
}