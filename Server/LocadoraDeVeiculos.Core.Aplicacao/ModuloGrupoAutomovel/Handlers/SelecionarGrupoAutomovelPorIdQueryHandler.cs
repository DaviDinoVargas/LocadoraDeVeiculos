using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Handlers
{
    public class SelecionarGrupoAutomovelPorIdQueryHandler
        : IRequestHandler<SelecionarGrupoAutomovelPorIdQuery, Result<SelecionarGrupoAutomovelPorIdResult>>
    {
        private readonly IRepositorioGrupoAutomovel _repositorioGrupoAutomovel;

        public SelecionarGrupoAutomovelPorIdQueryHandler(IRepositorioGrupoAutomovel repositorioGrupoAutomovel)
        {
            _repositorioGrupoAutomovel = repositorioGrupoAutomovel;
        }

        public async Task<Result<SelecionarGrupoAutomovelPorIdResult>> Handle(
            SelecionarGrupoAutomovelPorIdQuery query, CancellationToken cancellationToken)
        {
            var registroEncontrado = await _repositorioGrupoAutomovel.SelecionarPorIdAsync(query.Id);

            if (registroEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            var result = new SelecionarGrupoAutomovelPorIdResult(
                registroEncontrado.Id,
                registroEncontrado.Nome,
                registroEncontrado.Descricao
            );

            return Result.Ok(result);
        }
    }
}
