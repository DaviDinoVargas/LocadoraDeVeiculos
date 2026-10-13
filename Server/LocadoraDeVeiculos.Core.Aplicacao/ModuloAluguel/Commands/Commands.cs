using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands
{
    // Commands
    public record CadastrarAluguelCommand(
        Guid CondutorId,
        Guid AutomovelId,
        Guid ClienteId,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto,
        List<Guid> TaxasServicosIds,
        string? CupomCodigo = null
    ) : IRequest<Result<CadastrarAluguelResult>>, ISerializadoPorEmpresa;

    public record CadastrarAluguelResult(Guid Id);

    public record EditarAluguelCommand(
        Guid Id,
        Guid CondutorId,
        Guid AutomovelId,
        Guid ClienteId,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto,
        List<Guid> TaxasServicosIds,
        string? CupomCodigo = null
    ) : IRequest<Result<EditarAluguelResult>>, ISerializadoPorEmpresa;

    public record EditarAluguelResult(
        Guid Id,
        Guid CondutorId,
        Guid AutomovelId,
        Guid ClienteId,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        decimal ValorPrevisto,
        decimal ValorDesconto
    );

    public record IniciarAluguelCommand(Guid Id) : IRequest<Result<IniciarAluguelResult>>, ISerializadoPorEmpresa;

    public record IniciarAluguelResult(Guid Id, StatusAluguel Status);

    public record CancelarAluguelCommand(Guid Id) : IRequest<Result<CancelarAluguelResult>>, ISerializadoPorEmpresa;

    public record CancelarAluguelResult(Guid Id, StatusAluguel Status);

    public record ExcluirAluguelCommand(Guid Id) : IRequest<Result<ExcluirAluguelResult>>, ISerializadoPorEmpresa;

    public record ExcluirAluguelResult();

    // Queries
    public record SelecionarAlugueisQuery() : IRequest<Result<SelecionarAlugueisResult>>;

    public record SelecionarAlugueisResult(IReadOnlyList<SelecionarAlugueisDto> Registros);

    public record SelecionarAluguelPorIdQuery(Guid Id) : IRequest<Result<SelecionarAluguelPorIdResult>>;

    public record SelecionarAluguelPorIdResult(
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
        List<SelecionarTaxaServicoDto> TaxasServicos,
        decimal ValorDesconto,
        string? CupomCodigo
    );

    /// <summary>
    /// Calcula, sem gravar nada, quanto custaria um aluguel: diárias do plano do grupo do automóvel
    /// + taxas/serviços escolhidos, mais a caução vigente da empresa. O cupom (desconto) é
    /// consultado à parte em ValidarCupomQuery, usando ValorPrevisto como base.
    /// </summary>
    public record SimularAluguelQuery(
        Guid AutomovelId,
        DateTimeOffset DataSaida,
        DateTimeOffset DataRetornoPrevisto,
        List<Guid>? TaxasServicosIds
    ) : IRequest<Result<SimularAluguelResult>>;

    public record SimularAluguelResult(
        int Dias,
        string PlanoNome,
        decimal PrecoDiaria,
        decimal ValorDiarias,
        decimal ValorTaxas,
        decimal ValorPrevisto,
        decimal ValorCaucao
    );

    public record SelecionarAlugueisPorStatusQuery(string Status) : IRequest<Result<SelecionarAlugueisPorStatusResult>>;

    public record SelecionarAlugueisPorStatusResult(IReadOnlyList<SelecionarAlugueisDto> Registros);

    public record SelecionarAlugueisEmAbertoQuery() : IRequest<Result<SelecionarAlugueisEmAbertoResult>>;

    public record SelecionarAlugueisEmAbertoResult(IReadOnlyList<SelecionarAlugueisDto> Registros);

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