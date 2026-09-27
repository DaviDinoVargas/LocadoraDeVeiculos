using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Handlers
{
    public class ExcluirDesafioCupomCommandHandler : IRequestHandler<ExcluirDesafioCupomCommand, Result<ExcluirDesafioCupomResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioDesafioCupom _repositorioDesafioCupom;
        private readonly ILogger<ExcluirDesafioCupomCommandHandler> _logger;

        public ExcluirDesafioCupomCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioDesafioCupom repositorioDesafioCupom,
            ILogger<ExcluirDesafioCupomCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioDesafioCupom = repositorioDesafioCupom;
            _logger = logger;
        }

        public async Task<Result<ExcluirDesafioCupomResult>> Handle(
            ExcluirDesafioCupomCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var desafio = await _repositorioDesafioCupom.SelecionarPorIdAsync(command.Id);

                if (desafio is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                await _repositorioDesafioCupom.ExcluirAsync(desafio.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirDesafioCupomResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de desafio de cupom: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
