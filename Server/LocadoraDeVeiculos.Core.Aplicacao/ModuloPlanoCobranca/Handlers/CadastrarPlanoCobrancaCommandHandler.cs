using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Handlers
{
    public class CadastrarPlanoCobrancaCommandHandler
        : IRequestHandler<CadastrarPlanoCobrancaCommand, Result<CadastrarPlanoCobrancaResult>>
    {
        private readonly IRepositorioPlanoCobranca repo;
        private readonly LocadoraDeVeiculosDbContext db;
        private readonly ITenantProvider tenant;

        public CadastrarPlanoCobrancaCommandHandler(
            LocadoraDeVeiculosDbContext db,
            IRepositorioPlanoCobranca repo,
            ITenantProvider tenant)
        {
            this.db = db;
            this.repo = repo;
            this.tenant = tenant;
        }

        public async Task<Result<CadastrarPlanoCobrancaResult>> Handle(
            CadastrarPlanoCobrancaCommand command, CancellationToken cancellationToken)
        {
            var plano = new PlanoCobranca(
                command.GrupoAutomovelId,
                tenant.EmpresaId.GetValueOrDefault(),
                command.Nome,
                command.PrecoDiaria,
                command.PrecoPorKm,
                command.KmLivreLimite
            );

            await repo.CadastrarAsync(plano);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok(new CadastrarPlanoCobrancaResult(plano.Id));
        }
    }
}
