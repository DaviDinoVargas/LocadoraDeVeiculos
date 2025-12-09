using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands
{
    // Commands
    public record CadastrarCondutorCommand(
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId = null
    ) : IRequest<Result<CadastrarCondutorResult>>;

    public record CadastrarCondutorResult(Guid Id);

    public record EditarCondutorCommand(
        Guid Id,
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId = null
    ) : IRequest<Result<EditarCondutorResult>>;

    public record EditarCondutorResult(
        Guid Id,
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId
    );

    public record ExcluirCondutorCommand(Guid Id) : IRequest<Result<ExcluirCondutorResult>>;

    public record ExcluirCondutorResult();

    // Queries
    public record SelecionarCondutoresQuery() : IRequest<Result<SelecionarCondutoresResult>>;

    public record SelecionarCondutoresResult(IReadOnlyList<SelecionarCondutoresDto> Registros);

    public record SelecionarCondutorPorIdQuery(Guid Id) : IRequest<Result<SelecionarCondutorPorIdResult>>;

    public record SelecionarCondutorPorIdResult(
        Guid Id,
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId,
        string? ClienteNome
    );

    public record SelecionarCondutoresPorClienteQuery(Guid ClienteId) : IRequest<Result<SelecionarCondutoresPorClienteResult>>;

    public record SelecionarCondutoresPorClienteResult(IReadOnlyList<SelecionarCondutoresDto> Registros);

    // DTOs
    public record SelecionarCondutoresDto(
        Guid Id,
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId,
        string? ClienteNome
    );
}