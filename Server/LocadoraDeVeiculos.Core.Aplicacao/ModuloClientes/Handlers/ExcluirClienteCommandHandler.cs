using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Handlers
{
    public class ExcluirClienteCommandHandler
        : IRequestHandler<ExcluirClienteCommand, Result<ExcluirClienteResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioCliente _repositorioCliente;
        private readonly ILogger<ExcluirClienteCommandHandler> _logger;

        public ExcluirClienteCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioCliente repositorioCliente,
            ILogger<ExcluirClienteCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioCliente = repositorioCliente;
            _logger = logger;
        }

        public async Task<Result<ExcluirClienteResult>> Handle(
            ExcluirClienteCommand command, CancellationToken cancellationToken)
        {
            try
            {
                var cliente = await _repositorioCliente.SelecionarPorIdAsync(command.Id);

                if (cliente is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

                // Verificar se existe aluguel em aberto
                //if (await _repositorioCliente.ExisteAluguelEmAbertoAsync(command.Id))
                //    return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível excluir um cliente com aluguel em aberto."));

                await _repositorioCliente.ExcluirAsync(cliente.Id);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ExcluirClienteResult());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a exclusão de cliente: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}