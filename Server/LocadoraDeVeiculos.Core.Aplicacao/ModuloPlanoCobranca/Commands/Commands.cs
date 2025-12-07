using FluentResults;
using MediatR;

public record CadastrarPlanoCobrancaCommand(
    Guid GrupoAutomovelId,
    string Nome,
    decimal PrecoDiaria,
    decimal PrecoPorKm,
    int KmLivreLimite
) : IRequest<Result<CadastrarPlanoCobrancaResult>>;

public record CadastrarPlanoCobrancaResult(Guid Id);

public record EditarPlanoCobrancaCommand(
    Guid Id,
    Guid GrupoAutomovelId,
    string Nome,
    decimal PrecoDiaria,
    decimal PrecoPorKm,
    int KmLivreLimite
) : IRequest<Result<EditarPlanoCobrancaResult>>;

public record EditarPlanoCobrancaResult(
    string Nome, decimal PrecoDiaria, decimal PrecoPorKm, int KmLivreLimite);

public record ExcluirPlanoCobrancaCommand(Guid Id)
    : IRequest<Result<ExcluirPlanoCobrancaResult>>;

public record ExcluirPlanoCobrancaResult();
