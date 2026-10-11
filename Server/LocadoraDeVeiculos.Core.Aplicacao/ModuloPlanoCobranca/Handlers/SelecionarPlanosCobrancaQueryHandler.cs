using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Handlers
{
    public class SelecionarPlanosCobrancaQueryHandler
        : IRequestHandler<SelecionarPlanosCobrancaQuery, Result<SelecionarPlanosCobrancaResult>>
    {
        private readonly RepositorioPlanoCobrancaEmOrm repo;
        private readonly IRepositorioGrupoAutomovel repositorioGrupo;

        public SelecionarPlanosCobrancaQueryHandler(
            RepositorioPlanoCobrancaEmOrm repo,
            IRepositorioGrupoAutomovel repositorioGrupo)
        {
            this.repo = repo;
            this.repositorioGrupo = repositorioGrupo;
        }

        public async Task<Result<SelecionarPlanosCobrancaResult>> Handle(
            SelecionarPlanosCobrancaQuery query, CancellationToken cancellationToken)
        {
            var lista = await repo.SelecionarTodosAsync();

            // O plano só guarda o id do grupo; os nomes vêm numa única consulta (sem N+1) para a
            // lista mostrar "Econômico" em vez de um GUID.
            var nomesDosGrupos = (await repositorioGrupo.SelecionarTodosAsync())
                .ToDictionary(g => g.Id, g => g.Nome);

            var dtos = lista
                .Select(p => new SelecionarPlanosCobrancaDto(
                    p.Id,
                    p.Nome,
                    p.PrecoDiaria,
                    p.PrecoPorKm,
                    p.KmLivreLimite,
                    p.GrupoAutomovelId,
                    nomesDosGrupos.GetValueOrDefault(p.GrupoAutomovelId, string.Empty)
                ))
                .ToList();

            return Result.Ok(new SelecionarPlanosCobrancaResult(dtos));
        }
    }
}
