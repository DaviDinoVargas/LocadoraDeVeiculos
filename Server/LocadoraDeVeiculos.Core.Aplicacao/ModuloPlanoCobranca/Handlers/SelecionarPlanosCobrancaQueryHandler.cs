using FluentResults;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Handlers
{
    public class SelecionarPlanosCobrancaQueryHandler
        : IRequestHandler<SelecionarPlanosCobrancaQuery, Result<SelecionarPlanosCobrancaResult>>
    {
        private readonly RepositorioPlanoCobrancaEmOrm repo;

        public SelecionarPlanosCobrancaQueryHandler(RepositorioPlanoCobrancaEmOrm repo)
        {
            this.repo = repo;
        }

        public async Task<Result<SelecionarPlanosCobrancaResult>> Handle(
            SelecionarPlanosCobrancaQuery query, CancellationToken cancellationToken)
        {
            var lista = await repo.SelecionarTodosAsync();

            var dtos = lista
                .Select(p => new SelecionarPlanosCobrancaDto(
                    p.Id,
                    p.Nome,
                    p.PrecoDiaria,
                    p.PrecoPorKm,
                    p.KmLivreLimite
                ))
                .ToList();

            return Result.Ok(new SelecionarPlanosCobrancaResult(dtos));
        }
    }
}
