using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Handlers
{
    public class EditarDesafioCupomCommandHandler : IRequestHandler<EditarDesafioCupomCommand, Result<EditarDesafioCupomResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioDesafioCupom _repositorioDesafioCupom;
        private readonly IRepositorioCupom _repositorioCupom;
        private readonly IValidator<EditarDesafioCupomCommand> _validator;
        private readonly ILogger<EditarDesafioCupomCommandHandler> _logger;

        public EditarDesafioCupomCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioDesafioCupom repositorioDesafioCupom,
            IRepositorioCupom repositorioCupom,
            IValidator<EditarDesafioCupomCommand> validator,
            ILogger<EditarDesafioCupomCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioDesafioCupom = repositorioDesafioCupom;
            _repositorioCupom = repositorioCupom;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarDesafioCupomResult>> Handle(
            EditarDesafioCupomCommand command, CancellationToken cancellationToken)
        {
            var registroEncontrado = await _repositorioDesafioCupom.SelecionarPorIdAsync(command.Id);

            if (registroEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(resultadoValidacao.Errors.Select(e => e.ErrorMessage)));

            if (await _repositorioCupom.SelecionarPorIdAsync(command.CupomRecompensaId) is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.CupomRecompensaId));

            try
            {
                var desafioEditado = new DesafioCupom(
                    command.Nome,
                    command.Descricao,
                    command.MetaQuantidadeAlugueis,
                    command.PeriodoDias,
                    command.CupomRecompensaId
                )
                {
                    Ativo = command.Ativo
                };

                await _repositorioDesafioCupom.EditarAsync(command.Id, desafioEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarDesafioCupomResult(
                    command.Nome, command.Descricao, command.MetaQuantidadeAlugueis, command.PeriodoDias));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de desafio de cupom: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
