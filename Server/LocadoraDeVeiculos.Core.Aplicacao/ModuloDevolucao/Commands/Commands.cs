using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands
{
    // Commands
    public record RegistrarDevolucaoCommand(
        Guid AluguelId,
        DateTimeOffset DataDevolucao,
        decimal QuilometragemFinal,
        decimal CombustivelNoTanque,
        NivelCombustivel NivelCombustivel
    ) : IRequest<Result<RegistrarDevolucaoResult>>;

    public record RegistrarDevolucaoResult(
        Guid Id,
        Guid AluguelId,
        DateTimeOffset DataDevolucao,
        decimal QuilometragemFinal,
        decimal CombustivelNoTanque,
        decimal ValorMultas,
        decimal ValorAdicionalCombustivel,
        decimal ValorTotal
    );

    public record ExcluirDevolucaoCommand(Guid Id) : IRequest<Result<ExcluirDevolucaoResult>>;

    public record ExcluirDevolucaoResult();

    // Queries
    public record SelecionarDevolucoesQuery() : IRequest<Result<SelecionarDevolucoesResult>>;

    public record SelecionarDevolucoesResult(IReadOnlyList<SelecionarDevolucoesDto> Registros);

    public record SelecionarDevolucaoPorIdQuery(Guid Id) : IRequest<Result<SelecionarDevolucaoPorIdResult>>;

    public record SelecionarDevolucaoPorIdResult(
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

    public record SelecionarDevolucaoPorAluguelIdQuery(Guid AluguelId) : IRequest<Result<SelecionarDevolucaoPorAluguelIdResult>>;

    public record SelecionarDevolucaoPorAluguelIdResult(
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