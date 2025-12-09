using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers
{
    public class IniciarAluguelCommandHandler : IRequestHandler<IniciarAluguelCommand, Result<IniciarAluguelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly IValidator<IniciarAluguelCommand> _validator;
        private readonly ILogger<IniciarAluguelCommandHandler> _logger;

        public IniciarAluguelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioAluguel repositorioAluguel,
            IValidator<IniciarAluguelCommand> validator,
            ILogger<IniciarAluguelCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioAluguel = repositorioAluguel;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<IniciarAluguelResult>> Handle(
            IniciarAluguelCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            var aluguel = await _repositorioAluguel.SelecionarPorIdAsync(command.Id);

            if (aluguel is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            try
            {
                aluguel.IniciarAluguel();
                await _repositorioAluguel.EditarAsync(command.Id, aluguel);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new IniciarAluguelResult(aluguel.Id, aluguel.Status));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro ao iniciar o aluguel: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}