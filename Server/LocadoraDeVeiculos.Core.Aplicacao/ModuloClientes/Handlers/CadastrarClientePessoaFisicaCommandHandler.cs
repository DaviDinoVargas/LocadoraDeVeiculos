using Microsoft.EntityFrameworkCore;
using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
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
    public class CadastrarClientePessoaFisicaCommandHandler
        : IRequestHandler<CadastrarClientePessoaFisicaCommand, Result<CadastrarClienteResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCliente _repositorioCliente;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarClientePessoaFisicaCommand> _validator;
        private readonly ILogger<CadastrarClientePessoaFisicaCommandHandler> _logger;

        public CadastrarClientePessoaFisicaCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCliente repositorioCliente,
            ITenantProvider tenantProvider,
            IValidator<CadastrarClientePessoaFisicaCommand> validator,
            ILogger<CadastrarClientePessoaFisicaCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCliente = repositorioCliente;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarClienteResult>> Handle(
            CadastrarClientePessoaFisicaCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            // A empresa-cliente a que a pessoa se vincula precisa ser da própria locadora (o filtro por empresa do banco esconde as
            // de outras): sem isto, dava para ligar um cliente a dados de outra empresa.
            if (command.ClientePessoaJuridicaId.HasValue &&
                !await _appDbContext.Clientes.AnyAsync(c => c.Id == command.ClientePessoaJuridicaId.Value, cancellationToken))
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.ClientePessoaJuridicaId.Value));

            if (await _repositorioCliente.ExisteClienteComCpfAsync(command.Cpf))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um cliente com este CPF."));
            }

            try
            {
                var clientePF = new ClientePessoaFisica(
                    command.Nome,
                    command.Telefone,
                    command.Email,
                    command.Endereco,
                    command.Cpf,
                    command.Rg,
                    command.Cnh,
                    command.ValidadeCnh,
                    command.ClientePessoaJuridicaId
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioCliente.CadastrarAsync(clientePF);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarClienteResult(clientePF.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de cliente pessoa física: {Comando}.", command.GetType().Name);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}