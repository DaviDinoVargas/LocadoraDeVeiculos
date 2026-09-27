using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Handlers
{
    public class ExcluirParceiroCommandHandler : IRequestHandler<ExcluirParceiroCommand, Result<ExcluirParceiroResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioParceiro _repositorioParceiro;
        private readonly ILogger<ExcluirParceiroCommandHandler> _logger;

        public ExcluirParceiroCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioParceiro repositorioParceiro,
            ILogger<ExcluirParceiroCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioParceiro = repositorioParceiro;
            _logger = logger;
        }

        public async Task<Result<ExcluirParceiroResult>> Handle(
            ExcluirParceiroCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var parceiro = await _repositorioParceiro.SelecionarPorIdAsync(command.Id);

                if (parceiro is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                if (await _repositorioParceiro.ExisteCupomVinculadoAsync(command.Id))
                    return Result.Fail(ResultadosErro.ExclusaoBloqueadaErro(
                        "Não é possível excluir um parceiro com cupons vinculados. Desative-o em vez de excluir."));

                await _repositorioParceiro.ExcluirAsync(parceiro.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirParceiroResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de parceiro: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
