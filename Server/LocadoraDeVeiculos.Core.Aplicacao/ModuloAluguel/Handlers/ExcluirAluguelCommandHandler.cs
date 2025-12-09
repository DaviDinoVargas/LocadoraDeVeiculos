using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers
{
    public class ExcluirAluguelCommandHandler : IRequestHandler<ExcluirAluguelCommand, Result<ExcluirAluguelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly ILogger<ExcluirAluguelCommandHandler> _logger;

        public ExcluirAluguelCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioAluguel repositorioAluguel,
            ILogger<ExcluirAluguelCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioAluguel = repositorioAluguel;
            _logger = logger;
        }

        public async Task<Result<ExcluirAluguelResult>> Handle(
            ExcluirAluguelCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var aluguel = await _repositorioAluguel.SelecionarPorIdAsync(command.Id);

                if (aluguel is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                //if (!aluguel.PodeSerExcluido())
                //    return Result.Fail(ResultadosErro.RegistroInvalidoErro("Não é possível excluir um aluguel em andamento, concluído ou cancelado."));

                await _repositorioAluguel.ExcluirAsync(aluguel.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirAluguelResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de aluguel: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}