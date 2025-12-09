using System;

namespace LocadoraDeVeiculos.WebApi.Models.ModuloCondutor
{
    // Requests
    public record CadastrarCondutorRequest(
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId = null
    );

    public record EditarCondutorRequest(
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId = null
    );

    public record ExcluirCondutorRequest(Guid Id);

    // Responses
    public record CadastrarCondutorResponse(Guid Id);

    public record EditarCondutorResponse(
        Guid Id,
        string Nome,
        string Email,
        string Cpf,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        Guid? ClienteId
    );

    public record ExcluirCondutorResponse();

    public record SelecionarCondutoresResponse(IReadOnlyList<SelecionarCondutoresDto> Registros);

    public record SelecionarCondutorPorIdResponse(
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

    public record SelecionarCondutoresPorClienteResponse(IReadOnlyList<SelecionarCondutoresDto> Registros);

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