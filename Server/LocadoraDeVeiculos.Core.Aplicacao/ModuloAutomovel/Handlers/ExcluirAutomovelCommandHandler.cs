using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Handlers
{
    public class ExcluirAutomovelCommandHandler : IRequestHandler<ExcluirAutomovelCommand, Result<ExcluirAutomovelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioAutomovel _repositorioAutomovel;
        private readonly ILogger<ExcluirAutomovelCommandHandler> _logger;

        public ExcluirAutomovelCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioAutomovel repositorioAutomovel,
            ILogger<ExcluirAutomovelCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioAutomovel = repositorioAutomovel;
            _logger = logger;
        }

        public async Task<Result<ExcluirAutomovelResult>> Handle(
            ExcluirAutomovelCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var automovel = await _repositorioAutomovel.SelecionarPorIdAsync(command.Id);

                if (automovel is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                // Verificar se existe aluguel em aberto para este automóvel
                //if (await _repositorioAutomovel.ExisteAluguelEmAbertoAsync(command.Id))
                //    return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível excluir um automóvel com aluguel em aberto."));

                await _repositorioAutomovel.ExcluirAsync(automovel.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirAutomovelResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}