using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Handlers
{
    public class EditarCondutorCommandHandler : IRequestHandler<EditarCondutorCommand, Result<EditarCondutorResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCondutor _repositorioCondutor;
        private readonly IValidator<EditarCondutorCommand> _validator;
        private readonly ILogger<EditarCondutorCommandHandler> _logger;

        public EditarCondutorCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCondutor repositorioCondutor,
            IValidator<EditarCondutorCommand> validator,
            ILogger<EditarCondutorCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCondutor = repositorioCondutor;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarCondutorResult>> Handle(
            EditarCondutorCommand command, CancellationToken cancellationToken)
        {
            var condutorExistente = await _repositorioCondutor.SelecionarPorIdAsync(command.Id);

            if (condutorExistente is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            //if (await _repositorioCondutor.ExisteAluguelEmAbertoAsync(command.Id))
            //    return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível editar um condutor com aluguel em aberto."));

            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            if (await _repositorioCondutor.ExisteCondutorComCpfAsync(command.Cpf, command.Id))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um condutor com este CPF."));
            }

            if (await _repositorioCondutor.ExisteCondutorComCnhAsync(command.Cnh, command.Id))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um condutor com esta CNH."));
            }

            try
            {
                var condutorEditado = new Condutor(
                    command.Nome,
                    command.Email,
                    command.Cpf,
                    command.Cnh,
                    command.ValidadeCnh,
                    command.Telefone,
                    command.ClienteId
                );

                await _repositorioCondutor.EditarAsync(command.Id, condutorEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarCondutorResult(
                    command.Id,
                    command.Nome,
                    command.Email,
                    command.Cpf,
                    command.Cnh,
                    command.ValidadeCnh,
                    command.Telefone,
                    command.ClienteId
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de condutor: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}