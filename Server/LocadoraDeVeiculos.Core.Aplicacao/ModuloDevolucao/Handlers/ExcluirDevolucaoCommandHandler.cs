using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Handlers
{
    public class ExcluirDevolucaoCommandHandler : IRequestHandler<ExcluirDevolucaoCommand, Result<ExcluirDevolucaoResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioDevolucao _repositorioDevolucao;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly ILogger<ExcluirDevolucaoCommandHandler> _logger;

        public ExcluirDevolucaoCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioDevolucao repositorioDevolucao,
            IRepositorioAluguel repositorioAluguel,
            ILogger<ExcluirDevolucaoCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioDevolucao = repositorioDevolucao;
            _repositorioAluguel = repositorioAluguel;
            _logger = logger;
        }

        public async Task<Result<ExcluirDevolucaoResult>> Handle(
            ExcluirDevolucaoCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var devolucao = await _repositorioDevolucao.SelecionarPorIdAsync(command.Id);

                if (devolucao is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                // Buscar o aluguel relacionado
                var aluguel = await _repositorioAluguel.SelecionarPorIdAsync(devolucao.AluguelId);
                if (aluguel != null)
                {
                    // Reverter status do aluguel para EmAndamento
                    aluguel.Status = StatusAluguel.EmAndamento;
                    await _repositorioAluguel.EditarAsync(aluguel.Id, aluguel);
                }

                await _repositorioDevolucao.ExcluirAsync(devolucao.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirDevolucaoResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de devolução: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}