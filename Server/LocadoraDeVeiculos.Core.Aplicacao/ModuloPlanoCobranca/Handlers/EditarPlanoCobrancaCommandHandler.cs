using FluentResults;
using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Handlers
{
    public class EditarPlanoCobrancaCommandHandler
        : IRequestHandler<EditarPlanoCobrancaCommand, Result<EditarPlanoCobrancaResult>>
    {
        private readonly IRepositorioPlanoCobranca repo;
        private readonly LocadoraDeVeiculosDbContext db;
        private readonly ITenantProvider tenant;
        private readonly IValidator<EditarPlanoCobrancaCommand> validator;

        public EditarPlanoCobrancaCommandHandler(
            LocadoraDeVeiculosDbContext db,
            IRepositorioPlanoCobranca repo,
            ITenantProvider tenant,
            IValidator<EditarPlanoCobrancaCommand> validator)
        {
            this.db = db;
            this.repo = repo;
            this.tenant = tenant;
            this.validator = validator;
        }

        public async Task<Result<EditarPlanoCobrancaResult>> Handle(
            EditarPlanoCobrancaCommand command, CancellationToken cancellationToken)
        {
            var registro = await repo.SelecionarPorIdAsync(command.Id);

            if (registro is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            var valid = await validator.ValidateAsync(command);

            if (!valid.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                    valid.Errors.Select(x => x.ErrorMessage)));

            var editado = new PlanoCobranca(
                command.GrupoAutomovelId,
                tenant.EmpresaId.GetValueOrDefault(),
                command.Nome,
                command.PrecoDiaria,
                command.PrecoPorKm,
                command.KmLivreLimite
            );

            await repo.EditarAsync(command.Id, editado);
            await db.SaveChangesAsync(cancellationToken);

            return Result.Ok(new EditarPlanoCobrancaResult(
                editado.Nome,
                editado.PrecoDiaria,
                editado.PrecoPorKm,
                editado.KmLivreLimite
            ));
        }
    }
}
