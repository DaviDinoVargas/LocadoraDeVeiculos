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
    public class EditarClientePessoaJuridicaCommandHandler
        : IRequestHandler<EditarClientePessoaJuridicaCommand, Result<EditarClienteResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCliente _repositorioCliente;
        private readonly IValidator<EditarClientePessoaJuridicaCommand> _validator;
        private readonly ILogger<EditarClientePessoaJuridicaCommandHandler> _logger;

        public EditarClientePessoaJuridicaCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCliente repositorioCliente,
            IValidator<EditarClientePessoaJuridicaCommand> validator,
            ILogger<EditarClientePessoaJuridicaCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCliente = repositorioCliente;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarClienteResult>> Handle(
            EditarClientePessoaJuridicaCommand command, CancellationToken cancellationToken)
        {
            // Buscar registro existente
            var clienteExistente = await _repositorioCliente.SelecionarPessoaJuridicaPorIdAsync(command.Id);

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

            // Verificar duplicidade de CNPJ (excluindo o próprio)
            if (await _repositorioCliente.ExisteClienteComCnpjAsync(command.Cnpj, command.Id))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um cliente com este CNPJ."));
            }

            try
            {
                var clientePJEditado = new ClientePessoaJuridica(
                    command.Nome,
                    command.Telefone,
                    command.Email,
                    command.Endereco,
                    command.Cnpj,
                    command.NomeFantasia
                );

                await _repositorioCliente.EditarAsync(command.Id, clientePJEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarClienteResult(
                    command.Id,
                    command.Nome,
                    command.Cnpj,
                    "PessoaJuridica"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de cliente pessoa jurídica: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}