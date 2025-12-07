using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Handlers
{
    public class SelecionarPlanoCobrancaPorIdQueryHandler
        : IRequestHandler<SelecionarPlanoCobrancaPorIdQuery, Result<SelecionarPlanoCobrancaPorIdResult>>
    {
        private readonly RepositorioPlanoCobrancaEmOrm repo;

        public SelecionarPlanoCobrancaPorIdQueryHandler(RepositorioPlanoCobrancaEmOrm repo)
        {
            this.repo = repo;
        }

        public async Task<Result<SelecionarPlanoCobrancaPorIdResult>> Handle(
            SelecionarPlanoCobrancaPorIdQuery query, CancellationToken cancellationToken)
        {
            var registro = await repo.SelecionarPorIdAsync(query.Id);

            if (registro is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            var result = new SelecionarPlanoCobrancaPorIdResult(
                registro.Id,
                registro.GrupoAutomovelId,
                registro.Nome,
                registro.PrecoDiaria,
                registro.PrecoPorKm,
                registro.KmLivreLimite
            );

            return Result.Ok(result);
        }
    }
}
