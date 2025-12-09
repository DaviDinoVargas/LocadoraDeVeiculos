using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands
{
    // Commands para Pessoa Física
    public record CadastrarClientePessoaFisicaCommand(
        string Nome,
        string Cpf,
        string Rg,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        string Email,
        string Endereco,
        Guid? ClientePessoaJuridicaId = null
    ) : IRequest<Result<CadastrarClienteResult>>;

    public record EditarClientePessoaFisicaCommand(
        Guid Id,
        string Nome,
        string Cpf,
        string Rg,
        string Cnh,
        DateTime ValidadeCnh,
        string Telefone,
        string Email,
        string Endereco,
        Guid? ClientePessoaJuridicaId = null
    ) : IRequest<Result<EditarClienteResult>>;

    // Commands para Pessoa Jurídica
    public record CadastrarClientePessoaJuridicaCommand(
        string Nome,
        string Cnpj,
        string NomeFantasia,
        string Telefone,
        string Email,
        string Endereco
    ) : IRequest<Result<CadastrarClienteResult>>;

    public record EditarClientePessoaJuridicaCommand(
        Guid Id,
        string Nome,
        string Cnpj,
        string NomeFantasia,
        string Telefone,
        string Email,
        string Endereco
    ) : IRequest<Result<EditarClienteResult>>;

    // Commands comuns
    public record ExcluirClienteCommand(Guid Id) : IRequest<Result<ExcluirClienteResult>>;

    // Results
    public record CadastrarClienteResult(Guid Id);
    public record EditarClienteResult(Guid Id, string Nome, string Documento, string TipoCliente);
    public record ExcluirClienteResult();

    // Queries
    public record SelecionarClientesQuery() : IRequest<Result<SelecionarClientesResult>>;
    public record SelecionarClientesResult(IReadOnlyList<SelecionarClientesDto> Registros);

    public record SelecionarClientePorIdQuery(Guid Id) : IRequest<Result<SelecionarClientePorIdResult>>;
    public record SelecionarClientePorIdResult(
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

    public record SelecionarPessoasFisicasQuery() : IRequest<Result<SelecionarPessoasFisicasResult>>;
    public record SelecionarPessoasFisicasResult(IReadOnlyList<SelecionarPessoaFisicaDto> Registros);

    public record SelecionarPessoasJuridicasQuery() : IRequest<Result<SelecionarPessoasJuridicasResult>>;
    public record SelecionarPessoasJuridicasResult(IReadOnlyList<SelecionarPessoaJuridicaDto> Registros);

    // DTOs
    public record SelecionarClientesDto(
        Guid Id,
        string Nome,
        string Telefone,
        string Email,
        string TipoCliente,
        string DocumentoPrincipal
    );

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