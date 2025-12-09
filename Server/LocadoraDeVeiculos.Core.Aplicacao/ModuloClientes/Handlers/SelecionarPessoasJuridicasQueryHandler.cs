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
    public class SelecionarPessoasJuridicasQueryHandler
        : IRequestHandler<SelecionarPessoasJuridicasQuery, Result<SelecionarPessoasJuridicasResult>>
    {
        private readonly IRepositorioCliente _repositorioCliente;

        public SelecionarPessoasJuridicasQueryHandler(IRepositorioCliente repositorioCliente)
        {
            _repositorioCliente = repositorioCliente;
        }

        public async Task<Result<SelecionarPessoasJuridicasResult>> Handle(
            SelecionarPessoasJuridicasQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioCliente.SelecionarPessoasJuridicasAsync();

            var dtos = registros
                .Select(pj => new SelecionarPessoaJuridicaDto(
                    pj.Id,
                    pj.Nome,
                    pj.Cnpj,
                    pj.NomeFantasia,
                    pj.Telefone,
                    pj.Email,
                    pj.Endereco
                ))
                .ToImmutableList();

            var response = new SelecionarPessoasJuridicasResult(dtos);

            return Result.Ok(response);
        }
    }
}