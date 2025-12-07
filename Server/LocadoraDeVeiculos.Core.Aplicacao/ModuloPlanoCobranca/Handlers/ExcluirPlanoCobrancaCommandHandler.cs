using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Handlers
{
    public class ExcluirPlanoCobrancaCommandHandler
        : IRequestHandler<ExcluirPlanoCobrancaCommand, Result<ExcluirPlanoCobrancaResult>>
    {
        private readonly RepositorioPlanoCobrancaEmOrm repo;
        private readonly LocadoraDeVeiculosDbContext db;

        public ExcluirPlanoCobrancaCommandHandler(
            LocadoraDeVeiculosDbContext db,
            RepositorioPlanoCobrancaEmOrm repo)
        {
            this.db = db;
            this.repo = repo;
        }

        public async Task<Result<ExcluirPlanoCobrancaResult>> Handle(
            ExcluirPlanoCobrancaCommand command, CancellationToken cancellationToken)
        {
            var plano = await repo.SelecionarPorIdAsync(command.Id);

            if (plano is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            await repo.ExcluirAsync(command.Id);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok(new ExcluirPlanoCobrancaResult());
        }
    }
}
