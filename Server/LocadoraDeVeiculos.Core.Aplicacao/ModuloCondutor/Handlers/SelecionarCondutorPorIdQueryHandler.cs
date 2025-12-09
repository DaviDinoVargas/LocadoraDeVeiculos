using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Handlers
{
    public class SelecionarCondutorPorIdQueryHandler : IRequestHandler<SelecionarCondutorPorIdQuery, Result<SelecionarCondutorPorIdResult>>
    {
        private readonly IRepositorioCondutor _repositorioCondutor;

        public SelecionarCondutorPorIdQueryHandler(IRepositorioCondutor repositorioCondutor)
        {
            _repositorioCondutor = repositorioCondutor;
        }

        public async Task<Result<SelecionarCondutorPorIdResult>> Handle(
            SelecionarCondutorPorIdQuery query, CancellationToken cancellationToken)
        {
            var condutor = await _repositorioCondutor.SelecionarPorIdAsync(query.Id);

            if (condutor is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            var result = new SelecionarCondutorPorIdResult(
                condutor.Id,
                condutor.Nome,
                condutor.Email,
                condutor.Cpf,
                condutor.Cnh,
                condutor.ValidadeCnh,
                condutor.Telefone,
                condutor.ClienteId,
                condutor.Cliente?.Nome
            );

            return Result.Ok(result);
        }
    }
}