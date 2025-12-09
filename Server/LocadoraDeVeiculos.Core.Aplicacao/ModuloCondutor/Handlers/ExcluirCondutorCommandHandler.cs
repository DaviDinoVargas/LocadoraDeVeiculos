using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Handlers
{
    public class ExcluirCondutorCommandHandler : IRequestHandler<ExcluirCondutorCommand, Result<ExcluirCondutorResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioCondutor _repositorioCondutor;
        private readonly ILogger<ExcluirCondutorCommandHandler> _logger;

        public ExcluirCondutorCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioCondutor repositorioCondutor,
            ILogger<ExcluirCondutorCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioCondutor = repositorioCondutor;
            _logger = logger;
        }

        public async Task<Result<ExcluirCondutorResult>> Handle(
            ExcluirCondutorCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var condutor = await _repositorioCondutor.SelecionarPorIdAsync(command.Id);

                if (condutor is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                //if (await _repositorioCondutor.ExisteAluguelEmAbertoAsync(command.Id))
                //    return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível excluir um condutor com aluguel em aberto."));

                await _repositorioCondutor.ExcluirAsync(condutor.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirCondutorResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de condutor: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}