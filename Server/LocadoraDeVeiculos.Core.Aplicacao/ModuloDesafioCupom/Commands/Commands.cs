using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands
{
    // Commands
    public record CadastrarDesafioCupomCommand(
        string Nome,
        string Descricao,
        int MetaQuantidadeAlugueis,
        int PeriodoDias,
        Guid CupomRecompensaId
    ) : IRequest<Result<CadastrarDesafioCupomResult>>;

    public record CadastrarDesafioCupomResult(Guid Id);

    public record EditarDesafioCupomCommand(
        Guid Id,
        string Nome,
        string Descricao,
        int MetaQuantidadeAlugueis,
        int PeriodoDias,
        Guid CupomRecompensaId,
        bool Ativo
    ) : IRequest<Result<EditarDesafioCupomResult>>;

    public record EditarDesafioCupomResult(string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias);

    public record ExcluirDesafioCupomCommand(Guid Id) : IRequest<Result<ExcluirDesafioCupomResult>>;

    public record ExcluirDesafioCupomResult();

    // Queries
    public record SelecionarDesafiosCupomQuery() : IRequest<Result<SelecionarDesafiosCupomResult>>;

    public record SelecionarDesafiosCupomResult(IReadOnlyList<SelecionarDesafiosCupomDto> Registros);

    public record SelecionarDesafioCupomPorIdQuery(Guid Id) : IRequest<Result<SelecionarDesafioCupomPorIdResult>>;

    public record SelecionarDesafioCupomPorIdResult(
        Guid Id, string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias,
        Guid CupomRecompensaId, string CupomRecompensaCodigo, bool Ativo);

    /// <summary>
    /// Verifica, para um cliente específico, quais desafios ativos ele já cumpriu — devolvendo
    /// o código do cupom liberado em cada um para ele poder usar no próximo aluguel.
    /// </summary>
    public record VerificarDesafiosClienteQuery(Guid ClienteId) : IRequest<Result<VerificarDesafiosClienteResult>>;

    public record VerificarDesafiosClienteResult(IReadOnlyList<ProgressoDesafioDto> Progresso);

    public record ProgressoDesafioDto(
        Guid DesafioId, string Nome, int Meta, int Progresso, bool Cumprido, string? CupomLiberadoCodigo);

    // DTOs
    public record SelecionarDesafiosCupomDto(
        Guid Id, string Nome, string Descricao, int MetaQuantidadeAlugueis, int PeriodoDias,
        string CupomRecompensaCodigo, bool Ativo);
}
