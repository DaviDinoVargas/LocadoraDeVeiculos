using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands
{
    // Commands
    public record CadastrarGrupoAutomovelCommand(
        string Nome,
        string? Descricao
    ) : IRequest<Result<CadastrarGrupoAutomovelResult>>;

    public record CadastrarGrupoAutomovelResult(Guid Id);

    public record EditarGrupoAutomovelCommand(
        Guid Id,
        string Nome,
        string? Descricao
    ) : IRequest<Result<EditarGrupoAutomovelResult>>;

    public record EditarGrupoAutomovelResult(
        string Nome,
        string? Descricao
    );

    public record ExcluirGrupoAutomovelCommand(Guid Id) : IRequest<Result<ExcluirGrupoAutomovelResult>>;

    public record ExcluirGrupoAutomovelResult();

    // Queries
    public record SelecionarGruposAutomovelQuery() : IRequest<Result<SelecionarGruposAutomovelResult>>;

    public record SelecionarGruposAutomovelResult(IReadOnlyList<SelecionarGruposAutomovelDto> Registros);

    public record SelecionarGruposAutomovelDto(
        Guid Id,
        string Nome,
        string? Descricao
    );

    public record SelecionarGrupoAutomovelPorIdQuery(Guid Id) : IRequest<Result<SelecionarGrupoAutomovelPorIdResult>>;

    public record SelecionarGrupoAutomovelPorIdResult(
        Guid Id,
        string Nome,
        string? Descricao
    );
}