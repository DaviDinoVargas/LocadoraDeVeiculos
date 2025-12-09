using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
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
    public class CadastrarCondutorCommandHandler : IRequestHandler<CadastrarCondutorCommand, Result<CadastrarCondutorResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCondutor _repositorioCondutor;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarCondutorCommand> _validator;
        private readonly ILogger<CadastrarCondutorCommandHandler> _logger;

        public CadastrarCondutorCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCondutor repositorioCondutor,
            ITenantProvider tenantProvider,
            IValidator<CadastrarCondutorCommand> validator,
            ILogger<CadastrarCondutorCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCondutor = repositorioCondutor;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarCondutorResult>> Handle(
            CadastrarCondutorCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            if (await _repositorioCondutor.ExisteCondutorComCpfAsync(command.Cpf))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um condutor com este CPF."));
            }

            if (await _repositorioCondutor.ExisteCondutorComCnhAsync(command.Cnh))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um condutor com esta CNH."));
            }

            try
            {
                var condutor = new Condutor(
                    command.Nome,
                    command.Email,
                    command.Cpf,
                    command.Cnh,
                    command.ValidadeCnh,
                    command.Telefone,
                    command.ClienteId
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioCondutor.CadastrarAsync(condutor);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarCondutorResult(condutor.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de condutor: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}