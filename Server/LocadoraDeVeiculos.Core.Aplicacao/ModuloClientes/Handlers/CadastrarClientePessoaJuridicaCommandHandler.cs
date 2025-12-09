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
    public class CadastrarClientePessoaJuridicaCommandHandler
        : IRequestHandler<CadastrarClientePessoaJuridicaCommand, Result<CadastrarClienteResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCliente _repositorioCliente;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarClientePessoaJuridicaCommand> _validator;
        private readonly ILogger<CadastrarClientePessoaJuridicaCommandHandler> _logger;

        public CadastrarClientePessoaJuridicaCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCliente repositorioCliente,
            ITenantProvider tenantProvider,
            IValidator<CadastrarClientePessoaJuridicaCommand> validator,
            ILogger<CadastrarClientePessoaJuridicaCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCliente = repositorioCliente;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarClienteResult>> Handle(
            CadastrarClientePessoaJuridicaCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            if (await _repositorioCliente.ExisteClienteComCnpjAsync(command.Cnpj))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um cliente com este CNPJ."));
            }

            try
            {
                var clientePJ = new ClientePessoaJuridica(
                    command.Nome,
                    command.Telefone,
                    command.Email,
                    command.Endereco,
                    command.Cnpj,
                    command.NomeFantasia
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioCliente.CadastrarAsync(clientePJ);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarClienteResult(clientePJ.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de cliente pessoa jurídica: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}