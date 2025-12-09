using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Handlers
{
    public class EditarClientePessoaFisicaCommandHandler
        : IRequestHandler<EditarClientePessoaFisicaCommand, Result<EditarClienteResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCliente _repositorioCliente;
        private readonly IValidator<EditarClientePessoaFisicaCommand> _validator;
        private readonly ILogger<EditarClientePessoaFisicaCommandHandler> _logger;

        public EditarClientePessoaFisicaCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCliente repositorioCliente,
            IValidator<EditarClientePessoaFisicaCommand> validator,
            ILogger<EditarClientePessoaFisicaCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCliente = repositorioCliente;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarClienteResult>> Handle(
            EditarClientePessoaFisicaCommand command, CancellationToken cancellationToken)
        {
            // Buscar registro existente
            var clienteExistente = await _repositorioCliente.SelecionarPessoaFisicaPorIdAsync(command.Id);

            if (clienteExistente is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            // Verificar se existe aluguel em aberto
            //if (await _repositorioCliente.ExisteAluguelEmAbertoAsync(command.Id))
            //    return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível editar um cliente com aluguel em aberto."));

            // Validação do comando
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            // Verificar duplicidade de CPF (excluindo o próprio)
            if (await _repositorioCliente.ExisteClienteComCpfAsync(command.Cpf, command.Id))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um cliente com este CPF."));
            }

            try
            {
                var clientePFEditado = new ClientePessoaFisica(
                    command.Nome,
                    command.Telefone,
                    command.Email,
                    command.Endereco,
                    command.Cpf,
                    command.Rg,
                    command.Cnh,
                    command.ValidadeCnh,
                    command.ClientePessoaJuridicaId
                );

                await _repositorioCliente.EditarAsync(command.Id, clientePFEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarClienteResult(
                    command.Id,
                    command.Nome,
                    command.Cpf,
                    "PessoaFisica"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de cliente pessoa física: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}