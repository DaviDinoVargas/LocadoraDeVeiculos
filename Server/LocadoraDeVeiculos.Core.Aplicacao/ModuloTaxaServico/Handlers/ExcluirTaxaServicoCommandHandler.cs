using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Handlers
{
    public class ExcluirTaxaServicoCommandHandler : IRequestHandler<ExcluirTaxaServicoCommand, Result<ExcluirTaxaServicoResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;
        private readonly ILogger<ExcluirTaxaServicoCommandHandler> _logger;

        public ExcluirTaxaServicoCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioTaxaServico repositorioTaxaServico,
            ILogger<ExcluirTaxaServicoCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioTaxaServico = repositorioTaxaServico;
            _logger = logger;
        }

        public async Task<Result<ExcluirTaxaServicoResult>> Handle(
            ExcluirTaxaServicoCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var taxaServico = await _repositorioTaxaServico.SelecionarPorIdAsync(command.Id);

                if (taxaServico is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                // Verificar se existe aluguel vinculado
                //if (await _repositorioTaxaServico.ExisteAluguelVinculadoAsync(command.Id))
                //    return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível excluir uma taxa/serviço vinculada a um aluguel."));

                await _repositorioTaxaServico.ExcluirAsync(taxaServico.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirTaxaServicoResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de taxa/serviço: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}