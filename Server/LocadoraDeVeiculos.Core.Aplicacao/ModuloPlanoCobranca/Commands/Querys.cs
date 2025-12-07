using FluentResults;
using MediatR;

public record SelecionarPlanosCobrancaQuery()
    : IRequest<Result<SelecionarPlanosCobrancaResult>>;

public record SelecionarPlanosCobrancaDto(
    Guid Id, string Nome, decimal PrecoDiaria, decimal PrecoPorKm, int KmLivreLimite);

public record SelecionarPlanosCobrancaResult(
    IReadOnlyList<SelecionarPlanosCobrancaDto> Registros);

public record SelecionarPlanoCobrancaPorIdQuery(Guid Id)
    : IRequest<Result<SelecionarPlanoCobrancaPorIdResult>>;

public record SelecionarPlanoCobrancaPorIdResult(
    Guid Id, Guid GrupoAutomovelId, string Nome,
    decimal PrecoDiaria, decimal PrecoPorKm, int KmLivreLimite);
