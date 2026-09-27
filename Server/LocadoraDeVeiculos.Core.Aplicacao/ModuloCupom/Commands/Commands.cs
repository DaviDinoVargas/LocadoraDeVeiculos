using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using MediatR;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands
{
    // Commands
    public record CadastrarCupomCommand(
        string Codigo,
        string Descricao,
        TipoDesconto TipoDesconto,
        decimal ValorDesconto,
        DateTimeOffset ValidoAte,
        int? LimiteUsos,
        Guid? ParceiroId
    ) : IRequest<Result<CadastrarCupomResult>>;

    public record CadastrarCupomResult(Guid Id);

    public record EditarCupomCommand(
        Guid Id,
        string Codigo,
        string Descricao,
        TipoDesconto TipoDesconto,
        decimal ValorDesconto,
        DateTimeOffset ValidoAte,
        int? LimiteUsos,
        Guid? ParceiroId,
        bool Ativo
    ) : IRequest<Result<EditarCupomResult>>;

    public record EditarCupomResult(string Codigo, string Descricao, decimal ValorDesconto, DateTimeOffset ValidoAte);

    public record ExcluirCupomCommand(Guid Id) : IRequest<Result<ExcluirCupomResult>>;

    public record ExcluirCupomResult();

    // Queries
    public record SelecionarCuponsQuery() : IRequest<Result<SelecionarCuponsResult>>;

    public record SelecionarCuponsResult(IReadOnlyList<SelecionarCuponsDto> Registros);

    public record SelecionarCupomPorIdQuery(Guid Id) : IRequest<Result<SelecionarCupomPorIdResult>>;

    public record SelecionarCupomPorIdResult(
        Guid Id, string Codigo, string Descricao, TipoDesconto TipoDesconto, decimal ValorDesconto,
        DateTimeOffset ValidoAte, int? LimiteUsos, int UsosAtuais, Guid? ParceiroId, string? ParceiroNome, bool Ativo);

    /// <summary>
    /// Consulta somente-leitura: confere se um código de cupom pode ser usado agora e qual
    /// seria o desconto sobre um valor base — usada pelo front antes de fechar um aluguel.
    /// </summary>
    public record ValidarCupomQuery(string Codigo, decimal ValorBase) : IRequest<Result<ValidarCupomResult>>;

    public record ValidarCupomResult(bool Valido, string? MotivoInvalido, decimal ValorDesconto, decimal ValorComDesconto);

    // DTOs
    public record SelecionarCuponsDto(
        Guid Id, string Codigo, string Descricao, TipoDesconto TipoDesconto, decimal ValorDesconto,
        DateTimeOffset ValidoAte, int? LimiteUsos, int UsosAtuais, bool Ativo);
}
