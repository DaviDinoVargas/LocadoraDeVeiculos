using System;

namespace LocadoraDeVeiculos.WebApi.Models.ModuloCliente
{
    // Requests
    public record CadastrarClientePessoaFisicaRequest(
        string Nome,
        string Cpf,
        string Rg,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        string Email,
        string Endereco,
        Guid? ClientePessoaJuridicaId = null
    );

    public record CadastrarClientePessoaJuridicaRequest(
        string Nome,
        string Cnpj,
        string NomeFantasia,
        string Telefone,
        string Email,
        string Endereco
    );

    public record EditarClientePessoaFisicaRequest(
        string Nome,
        string Cpf,
        string Rg,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        string Email,
        string Endereco,
        Guid? ClientePessoaJuridicaId = null
    );

    public record EditarClientePessoaJuridicaRequest(
        string Nome,
        string Cnpj,
        string NomeFantasia,
        string Telefone,
        string Email,
        string Endereco
    );

    public record ExcluirClienteRequest(Guid Id);

    // Responses
    public record CadastrarClienteResponse(Guid Id);
    public record EditarClienteResponse(Guid Id, string Nome, string Documento, string TipoCliente);
    public record ExcluirClienteResponse();

    public record SelecionarClientesResponse(IReadOnlyList<SelecionarClientesDto> Registros);
    public record SelecionarClientesDto(
        Guid Id,
        string Nome,
        string Telefone,
        string Email,
        string TipoCliente,
        string DocumentoPrincipal
    );

    public record SelecionarClientePorIdResponse(
        Guid Id,
        string Nome,
        string Telefone,
        string Email,
        string Endereco,
        string TipoCliente,
        string? Cpf,
        string? Rg,
        string? Cnh,
        DateTime? ValidadeCnh,
        Guid? ClientePessoaJuridicaId,
        string? ClientePessoaJuridicaNome,
        string? Cnpj,
        string? NomeFantasia
    );

    public record SelecionarPessoasFisicasResponse(IReadOnlyList<SelecionarPessoaFisicaDto> Registros);
    public record SelecionarPessoaFisicaDto(
        Guid Id,
        string Nome,
        string Cpf,
        string Rg,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        string Email,
        string Endereco,
        Guid? ClientePessoaJuridicaId,
        string? ClientePessoaJuridicaNome
    );

    public record SelecionarPessoasJuridicasResponse(IReadOnlyList<SelecionarPessoaJuridicaDto> Registros);
    public record SelecionarPessoaJuridicaDto(
        Guid Id,
        string Nome,
        string Cnpj,
        string NomeFantasia,
        string Telefone,
        string Email,
        string Endereco
    );
}