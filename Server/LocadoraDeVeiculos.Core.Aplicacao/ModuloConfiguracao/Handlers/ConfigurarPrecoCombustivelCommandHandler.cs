using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Handlers
{
    public class ConfigurarPrecoCombustivelCommandHandler : IRequestHandler<ConfigurarPrecoCombustivelCommand, Result<ConfigurarPrecoCombustivelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioConfiguracao _repositorioConfiguracao;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<ConfigurarPrecoCombustivelCommand> _validator;
        private readonly ILogger<ConfigurarPrecoCombustivelCommandHandler> _logger;

        public ConfigurarPrecoCombustivelCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioConfiguracao repositorioConfiguracao,
            ITenantProvider tenantProvider,
            IValidator<ConfigurarPrecoCombustivelCommand> validator,
            ILogger<ConfigurarPrecoCombustivelCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioConfiguracao = repositorioConfiguracao;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<ConfigurarPrecoCombustivelResult>> Handle(
            ConfigurarPrecoCombustivelCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            try
            {
                var empresaId = _tenantProvider.EmpresaId.GetValueOrDefault();
                var configuracaoExistente = await _repositorioConfiguracao.SelecionarPorEmpresaIdAsync(empresaId);

                Configuracao configuracao;

                if (configuracaoExistente is null)
                {
                    // Criar nova configuração
                    configuracao = new Configuracao(empresaId, command.PrecoCombustivel);
                    await _repositorioConfiguracao.CadastrarAsync(configuracao);
                }
                else
                {
                    // Atualizar configuração existente
                    configuracaoExistente.PrecoCombustivel = command.PrecoCombustivel;
                    await _repositorioConfiguracao.EditarAsync(configuracaoExistente.Id, configuracaoExistente);
                    configuracao = configuracaoExistente;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ConfigurarPrecoCombustivelResult(
                    configuracao.Id,
                    configuracao.PrecoCombustivel,
                    configuracao.ValorGarantia
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a configuração do preço do combustível: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}