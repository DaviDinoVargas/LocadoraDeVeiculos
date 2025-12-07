using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using MediatR;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Handlers
{
    public class SelecionarGruposAutomovelQueryHandler
        : IRequestHandler<SelecionarGruposAutomovelQuery, Result<SelecionarGruposAutomovelResult>>
    {
        private readonly IRepositorioGrupoAutomovel _repositorioGrupoAutomovel;

        public SelecionarGruposAutomovelQueryHandler(IRepositorioGrupoAutomovel repositorioGrupoAutomovel)
        {
            _repositorioGrupoAutomovel = repositorioGrupoAutomovel;
        }

        public async Task<Result<SelecionarGruposAutomovelResult>> Handle(
            SelecionarGruposAutomovelQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioGrupoAutomovel.SelecionarTodosAsync();

            var dtos = registros
                .Select(r => new SelecionarGruposAutomovelDto(
                    r.Id,
                    r.Nome,
                    r.Descricao
                ))
                .ToImmutableList();

            var response = new SelecionarGruposAutomovelResult(dtos);

            return Result.Ok(response);
        }
    }
}
