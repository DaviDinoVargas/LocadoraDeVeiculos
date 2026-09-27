using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using MediatR;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands
{
    // Commands
    public record CadastrarParceiroCommand(
        string Nome,
        string Cnpj,
        CategoriaParceiro Categoria
    ) : IRequest<Result<CadastrarParceiroResult>>;

    public record CadastrarParceiroResult(Guid Id);

    public record EditarParceiroCommand(
        Guid Id,
        string Nome,
        string Cnpj,
        CategoriaParceiro Categoria,
        bool Ativo
    ) : IRequest<Result<EditarParceiroResult>>;

    public record EditarParceiroResult(string Nome, string Cnpj, CategoriaParceiro Categoria, bool Ativo);

    public record ExcluirParceiroCommand(Guid Id) : IRequest<Result<ExcluirParceiroResult>>;

    public record ExcluirParceiroResult();

    // Queries
    public record SelecionarParceirosQuery() : IRequest<Result<SelecionarParceirosResult>>;

    public record SelecionarParceirosResult(IReadOnlyList<SelecionarParceirosDto> Registros);

    public record SelecionarParceiroPorIdQuery(Guid Id) : IRequest<Result<SelecionarParceiroPorIdResult>>;

    public record SelecionarParceiroPorIdResult(Guid Id, string Nome, string Cnpj, CategoriaParceiro Categoria, bool Ativo);

    // DTOs
    public record SelecionarParceirosDto(Guid Id, string Nome, string Cnpj, CategoriaParceiro Categoria, bool Ativo);
}
