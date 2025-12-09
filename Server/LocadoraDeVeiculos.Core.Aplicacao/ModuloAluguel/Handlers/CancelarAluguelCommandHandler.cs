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
    public class CancelarAluguelCommandHandler : IRequestHandler<CancelarAluguelCommand, Result<CancelarAluguelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly IValidator<CancelarAluguelCommand> _validator;
        private readonly ILogger<CancelarAluguelCommandHandler> _logger;

        public CancelarAluguelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioAluguel repositorioAluguel,
            IValidator<CancelarAluguelCommand> validator,
            ILogger<CancelarAluguelCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioAluguel = repositorioAluguel;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CancelarAluguelResult>> Handle(
            CancelarAluguelCommand command, CancellationToken cancellationToken)
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

            //if (aluguel.Status == StatusAluguel.EmAndamento)
            //    return Result.Fail(ResultadosErro.RegistroInvalidoErro("Não é possível cancelar um aluguel em andamento."));

            //if (aluguel.Status == StatusAluguel.Concluido)
            //    return Result.Fail(ResultadosErro.RegistroInvalidoErro("Não é possível cancelar um aluguel já concluído."));

            try
            {
                aluguel.Status = Dominio.ModuloAluguel.StatusAluguel.Cancelado;
                await _repositorioAluguel.EditarAsync(command.Id, aluguel);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CancelarAluguelResult(aluguel.Id, aluguel.Status));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro ao cancelar o aluguel: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}