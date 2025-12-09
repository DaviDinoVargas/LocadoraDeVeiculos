using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands
{
    // Commands
    public record CadastrarTaxaServicoCommand(
        string Nome,
        decimal Preco,
        TipoCalculo TipoCalculo
    ) : IRequest<Result<CadastrarTaxaServicoResult>>;

    public record CadastrarTaxaServicoResult(Guid Id);

    public record EditarTaxaServicoCommand(
        Guid Id,
        string Nome,
        decimal Preco,
        TipoCalculo TipoCalculo
    ) : IRequest<Result<EditarTaxaServicoResult>>;

    public record EditarTaxaServicoResult(
        string Nome,
        decimal Preco,
        TipoCalculo TipoCalculo
    );

    public record ExcluirTaxaServicoCommand(Guid Id) : IRequest<Result<ExcluirTaxaServicoResult>>;

    public record ExcluirTaxaServicoResult();

    // Queries
    public record SelecionarTaxasServicoQuery() : IRequest<Result<SelecionarTaxasServicoResult>>;

    public record SelecionarTaxasServicoResult(IReadOnlyList<SelecionarTaxasServicoDto> Registros);

    public record SelecionarTaxaServicoPorIdQuery(Guid Id) : IRequest<Result<SelecionarTaxaServicoPorIdResult>>;

    public record SelecionarTaxaServicoPorIdResult(
        Guid Id,
        string Nome,
        decimal Preco,
        TipoCalculo TipoCalculo
    );

    public record SelecionarTaxasServicoPorTipoQuery(TipoCalculo TipoCalculo) : IRequest<Result<SelecionarTaxasServicoPorTipoResult>>;

    public record SelecionarTaxasServicoPorTipoResult(IReadOnlyList<SelecionarTaxasServicoDto> Registros);

    // DTOs
    public record SelecionarTaxasServicoDto(
        Guid Id,
        string Nome,
        decimal Preco,
        string TipoCalculo
    );
}