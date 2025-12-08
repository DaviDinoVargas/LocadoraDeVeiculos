using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands
{
    // Commands
    public record CadastrarAutomovelCommand(
        string Placa,
        string Marca,
        string Cor,
        string Modelo,
        TipoCombustivel TipoCombustivel,
        decimal CapacidadeTanque,
        int Ano,
        string? Foto,
        Guid GrupoAutomovelId
    ) : IRequest<Result<CadastrarAutomovelResult>>;

    public record CadastrarAutomovelResult(Guid Id);

    public record EditarAutomovelCommand(
        Guid Id,
        string Placa,
        string Marca,
        string Cor,
        string Modelo,
        TipoCombustivel TipoCombustivel,
        decimal CapacidadeTanque,
        int Ano,
        string? Foto,
        Guid GrupoAutomovelId
    ) : IRequest<Result<EditarAutomovelResult>>;

    public record EditarAutomovelResult(
        string Placa,
        string Marca,
        string Cor,
        string Modelo,
        TipoCombustivel TipoCombustivel,
        decimal CapacidadeTanque,
        int Ano,
        string? Foto,
        Guid GrupoAutomovelId
    );

    public record ExcluirAutomovelCommand(Guid Id) : IRequest<Result<ExcluirAutomovelResult>>;

    public record ExcluirAutomovelResult();

    // Queries
    public record SelecionarAutomoveisQuery() : IRequest<Result<SelecionarAutomoveisResult>>;

    public record SelecionarAutomoveisResult(IReadOnlyList<SelecionarAutomoveisDto> Registros);

    public record SelecionarAutomoveisDto(
        Guid Id,
        string Placa,
        string Marca,
        string Cor,
        string Modelo,
        TipoCombustivel TipoCombustivel,
        decimal CapacidadeTanque,
        int Ano,
        string? Foto,
        Guid GrupoAutomovelId,
        string GrupoAutomovelNome
    );

    public record SelecionarAutomovelPorIdQuery(Guid Id) : IRequest<Result<SelecionarAutomovelPorIdResult>>;

    public record SelecionarAutomovelPorIdResult(
        Guid Id,
        string Placa,
        string Marca,
        string Cor,
        string Modelo,
        TipoCombustivel TipoCombustivel,
        decimal CapacidadeTanque,
        int Ano,
        string? Foto,
        Guid GrupoAutomovelId,
        string GrupoAutomovelNome
    );

    public record SelecionarAutomoveisPorGrupoQuery(Guid GrupoId) : IRequest<Result<SelecionarAutomoveisPorGrupoResult>>;

    public record SelecionarAutomoveisPorGrupoResult(IReadOnlyList<SelecionarAutomoveisDto> Registros);
}