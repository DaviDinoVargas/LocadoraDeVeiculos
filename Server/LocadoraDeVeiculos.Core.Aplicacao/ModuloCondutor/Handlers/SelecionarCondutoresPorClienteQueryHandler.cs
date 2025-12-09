using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using MediatR;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Handlers
{
    public class SelecionarCondutoresPorClienteQueryHandler : IRequestHandler<SelecionarCondutoresPorClienteQuery, Result<SelecionarCondutoresPorClienteResult>>
    {
        private readonly IRepositorioCondutor _repositorioCondutor;

        public SelecionarCondutoresPorClienteQueryHandler(IRepositorioCondutor repositorioCondutor)
        {
            _repositorioCondutor = repositorioCondutor;
        }

        public async Task<Result<SelecionarCondutoresPorClienteResult>> Handle(
            SelecionarCondutoresPorClienteQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioCondutor.SelecionarPorClienteAsync(query.ClienteId);

            var dtos = registros
                .Select(c => new SelecionarCondutoresDto(
                    c.Id,
                    c.Nome,
                    c.Email,
                    c.Cpf,
                    c.Cnh,
                    c.ValidadeCnh,
                    c.Telefone,
                    c.ClienteId,
                    c.Cliente?.Nome
                ))
                .ToImmutableList();

            var response = new SelecionarCondutoresPorClienteResult(dtos);

            return Result.Ok(response);
        }
    }
}