using System;

namespace LocadoraDeVeiculos.WebApi.Models.ModuloDevolucao
{
    // Requests
    public record RegistrarDevolucaoRequest(
        Guid AluguelId,
        DateTimeOffset DataDevolucao,
        decimal QuilometragemFinal,
        decimal CombustivelNoTanque,
        string NivelCombustivel
    );

    public record ExcluirDevolucaoRequest(Guid Id);

    // Responses
    public record RegistrarDevolucaoResponse(
        Guid Id,
        Guid AluguelId,
        DateTimeOffset DataDevolucao,
        decimal QuilometragemFinal,
        decimal CombustivelNoTanque,
        decimal ValorMultas,
        decimal ValorAdicionalCombustivel,
        decimal ValorTotal
    );

    public record ExcluirDevolucaoResponse();

    public record SelecionarDevolucoesResponse(IReadOnlyList<SelecionarDevolucoesDto> Registros);

    public record SelecionarDevolucaoPorIdResponse(
        Guid Id,
        Guid AluguelId,
        string CondutorNome,
        string AutomovelPlaca,
        string ClienteNome,
        DateTimeOffset DataDevolucao,
        decimal QuilometragemFinal,
        decimal CombustivelNoTanque,
        string NivelCombustivel,
        decimal ValorMultas,
        decimal ValorAdicionalCombustivel,
        decimal ValorTotal
    );

    public record SelecionarDevolucaoPorAluguelIdResponse(
        Guid Id,
        Guid AluguelId,
        DateTimeOffset DataDevolucao,
        decimal QuilometragemFinal,
        decimal CombustivelNoTanque,
        string NivelCombustivel,
        decimal ValorMultas,
        decimal ValorAdicionalCombustivel,
        decimal ValorTotal
    );

    // DTOs
    public record SelecionarDevolucoesDto(
        Guid Id,
        string CondutorNome,
        string AutomovelPlaca,
        string ClienteNome,
        DateTimeOffset DataDevolucao,
        decimal QuilometragemFinal,
        decimal ValorTotal
    );
}