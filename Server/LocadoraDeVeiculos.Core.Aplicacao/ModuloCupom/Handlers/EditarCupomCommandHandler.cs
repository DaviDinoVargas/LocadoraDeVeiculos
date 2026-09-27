using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Handlers
{
    public class EditarCupomCommandHandler : IRequestHandler<EditarCupomCommand, Result<EditarCupomResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCupom _repositorioCupom;
        private readonly IValidator<EditarCupomCommand> _validator;
        private readonly ILogger<EditarCupomCommandHandler> _logger;

        public EditarCupomCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCupom repositorioCupom,
            IValidator<EditarCupomCommand> validator,
            ILogger<EditarCupomCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCupom = repositorioCupom;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarCupomResult>> Handle(
            EditarCupomCommand command, CancellationToken cancellationToken)
        {
            var registroEncontrado = await _repositorioCupom.SelecionarPorIdAsync(command.Id);

            if (registroEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            if (await _repositorioCupom.ExisteAluguelVinculadoAsync(command.Id))
                return Result.Fail(ResultadosErro.ExclusaoBloqueadaErro(
                    "Não é possível editar um cupom em uso por um aluguel em aberto."));

            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(resultadoValidacao.Errors.Select(e => e.ErrorMessage)));

            if (await _repositorioCupom.ExisteCupomComCodigoAsync(command.Codigo, command.Id))
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um cupom com este código."));

            try
            {
                var cupomEditado = new Cupom(
                    command.Codigo,
                    command.Descricao,
                    command.TipoDesconto,
                    command.ValorDesconto,
                    command.ValidoAte,
                    command.LimiteUsos,
                    command.ParceiroId
                )
                {
                    Ativo = command.Ativo
                };

                await _repositorioCupom.EditarAsync(command.Id, cupomEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarCupomResult(command.Codigo, command.Descricao, command.ValorDesconto, command.ValidoAte));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de cupom: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
