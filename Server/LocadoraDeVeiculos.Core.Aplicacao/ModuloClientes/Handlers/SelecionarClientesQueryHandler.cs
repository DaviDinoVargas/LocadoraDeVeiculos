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
    public class SelecionarClientesQueryHandler
        : IRequestHandler<SelecionarClientesQuery, Result<SelecionarClientesResult>>
    {
        private readonly IRepositorioCliente _repositorioCliente;

        public SelecionarClientesQueryHandler(IRepositorioCliente repositorioCliente)
        {
            _repositorioCliente = repositorioCliente;
        }

        public async Task<Result<SelecionarClientesResult>> Handle(
            SelecionarClientesQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioCliente.SelecionarTodosAsync();

            var dtos = registros
                .Select(c => c switch
                {
                    ClientePessoaFisica pf => new SelecionarClientesDto(
                        pf.Id,
                        pf.Nome,
                        pf.Telefone,
                        pf.Email,
                        "PessoaFisica",
                        pf.Cpf
                    ),
                    ClientePessoaJuridica pj => new SelecionarClientesDto(
                        pj.Id,
                        pj.Nome,
                        pj.Telefone,
                        pj.Email,
                        "PessoaJuridica",
                        pj.Cnpj
                    ),
                    _ => throw new InvalidOperationException("Tipo de cliente desconhecido")
                })
                .ToImmutableList();

            var response = new SelecionarClientesResult(dtos);

            return Result.Ok(response);
        }
    }
}