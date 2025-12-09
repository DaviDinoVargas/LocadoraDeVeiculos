using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using MediatR;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Handlers
{
    public class SelecionarPessoasFisicasQueryHandler
        : IRequestHandler<SelecionarPessoasFisicasQuery, Result<SelecionarPessoasFisicasResult>>
    {
        private readonly IRepositorioCliente _repositorioCliente;

        public SelecionarPessoasFisicasQueryHandler(IRepositorioCliente repositorioCliente)
        {
            _repositorioCliente = repositorioCliente;
        }

        public async Task<Result<SelecionarPessoasFisicasResult>> Handle(
            SelecionarPessoasFisicasQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioCliente.SelecionarPessoasFisicasAsync();

            var dtos = registros
                .Select(pf => new SelecionarPessoaFisicaDto(
                    pf.Id,
                    pf.Nome,
                    pf.Cpf,
                    pf.Rg,
                    pf.Cnh,
                    pf.ValidadeCnh.GetValueOrDefault(),
                    pf.Telefone,
                    pf.Email,
                    pf.Endereco,
                    pf.ClientePessoaJuridicaId,
                    pf.ClientePessoaJuridica?.Nome
                ))
                .ToImmutableList();

            var response = new SelecionarPessoasFisicasResult(dtos);

            return Result.Ok(response);
        }
    }
}