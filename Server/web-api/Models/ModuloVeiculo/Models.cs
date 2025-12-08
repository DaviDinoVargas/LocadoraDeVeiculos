using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.WebApi.Models.ModuloAutomovel
{
    public record CadastrarAutomovelRequest(
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

    public record CadastrarAutomovelResponse(Guid Id);

    public record EditarAutomovelRequest(
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

    public record EditarAutomovelResponse(
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

    public record ExcluirAutomovelRequest(Guid Id);

    public record ExcluirAutomovelResponse();

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
        Guid GrupoAutomovelId
    );

    public record SelecionarAutomoveisResponse(
        IReadOnlyList<SelecionarAutomoveisDto> Registros
    );

    public record SelecionarAutomovelPorIdResponse(
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

    public record SelecionarAutomoveisPorGrupoResponse(
        IReadOnlyList<SelecionarAutomoveisDto> Registros
    );
}
