using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Handlers
{
    public class ExcluirGrupoAutomovelCommandHandler
        : IRequestHandler<ExcluirGrupoAutomovelCommand, Result<ExcluirGrupoAutomovelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioGrupoAutomovel _repositorioGrupoAutomovel;
        private readonly ILogger<ExcluirGrupoAutomovelCommandHandler> _logger;

        public ExcluirGrupoAutomovelCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioGrupoAutomovel repositorioGrupoAutomovel,
            ILogger<ExcluirGrupoAutomovelCommandHandler> logger
        )
        {
            _dbContext = dbContext;
            _repositorioGrupoAutomovel = repositorioGrupoAutomovel;
            _logger = logger;
        }

        public async Task<Result<ExcluirGrupoAutomovelResult>> Handle(
            ExcluirGrupoAutomovelCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var grupoAutomovel = await _repositorioGrupoAutomovel.SelecionarPorIdAsync(command.Id);

                if (grupoAutomovel is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                if (await _repositorioGrupoAutomovel.ExisteAutomovelVinculadoAsync(command.Id))
                    return Result.Fail(ResultadosErro.ExclusaoBloqueadaErro(
                        "Não é possível excluir o grupo de automóvel pois está sendo utilizado em automóveis."));

                if (await _repositorioGrupoAutomovel.ExistePlanoCobrancaVinculadoAsync(command.Id))
                    return Result.Fail(ResultadosErro.ExclusaoBloqueadaErro(
                        "Não é possível excluir o grupo de automóvel pois está sendo utilizado em planos de cobrança."));

                await _repositorioGrupoAutomovel.ExcluirAsync(grupoAutomovel.Id);

                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirGrupoAutomovelResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
