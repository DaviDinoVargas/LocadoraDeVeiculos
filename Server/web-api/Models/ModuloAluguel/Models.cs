using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.WebApi.Models.ModuloAluguel
{
    // Requests
    public record CadastrarAluguelRequest(
        Guid CondutorId,
        Guid AutomovelId,
        Guid ClienteId,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto,
        List<Guid> TaxasServicosIds
    );

    public record EditarAluguelRequest(
        Guid CondutorId,
        Guid AutomovelId,
        Guid ClienteId,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto,
        List<Guid> TaxasServicosIds
    );

    public record IniciarAluguelRequest(Guid Id);

    public record CancelarAluguelRequest(Guid Id);

    public record ExcluirAluguelRequest(Guid Id);

    // Responses
    public record CadastrarAluguelResponse(Guid Id);

    public record EditarAluguelResponse(
        Guid Id,
        Guid CondutorId,
        Guid AutomovelId,
        Guid ClienteId,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto
    );

    public record IniciarAluguelResponse(Guid Id, string Status);

    public record CancelarAluguelResponse(Guid Id, string Status);

    public record ExcluirAluguelResponse();

    public record SelecionarAlugueisResponse(IReadOnlyList<SelecionarAlugueisDto> Registros);

    public record SelecionarAluguelPorIdResponse(
        Guid Id,
        Guid CondutorId,
        string CondutorNome,
        Guid AutomovelId,
        string AutomovelPlaca,
        Guid ClienteId,
        string ClienteNome,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto,
        decimal ValorCaucao,
        string Status,
        List<SelecionarTaxaServicoDto> TaxasServicos
    );

    public record SelecionarAlugueisPorStatusResponse(IReadOnlyList<SelecionarAlugueisDto> Registros);

    public record SelecionarAlugueisEmAbertoResponse(IReadOnlyList<SelecionarAlugueisDto> Registros);

    // DTOs
    public record SelecionarAlugueisDto(
        Guid Id,
        string CondutorNome,
        string AutomovelPlaca,
        string ClienteNome,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto,
        string Status
    );

    public record SelecionarTaxaServicoDto(
        Guid Id,
        string Nome,
        decimal Preco,
        string TipoCalculo
    );
}