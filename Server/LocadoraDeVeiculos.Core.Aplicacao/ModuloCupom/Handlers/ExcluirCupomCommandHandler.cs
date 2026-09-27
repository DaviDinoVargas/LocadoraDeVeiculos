using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Handlers
{
    public class ExcluirCupomCommandHandler : IRequestHandler<ExcluirCupomCommand, Result<ExcluirCupomResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioCupom _repositorioCupom;
        private readonly ILogger<ExcluirCupomCommandHandler> _logger;

        public ExcluirCupomCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioCupom repositorioCupom,
            ILogger<ExcluirCupomCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioCupom = repositorioCupom;
            _logger = logger;
        }

        public async Task<Result<ExcluirCupomResult>> Handle(
            ExcluirCupomCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var cupom = await _repositorioCupom.SelecionarPorIdAsync(command.Id);

                if (cupom is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                if (await _repositorioCupom.ExisteAluguelVinculadoAsync(command.Id))
                    return Result.Fail(ResultadosErro.ExclusaoBloqueadaErro(
                        "Não é possível excluir um cupom em uso por um aluguel em aberto."));

                await _repositorioCupom.ExcluirAsync(cupom.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirCupomResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de cupom: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
