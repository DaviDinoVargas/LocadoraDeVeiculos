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
    /// <summary>
    /// Define a caução (Configuracao.ValorGarantia) cobrada nos NOVOS aluguéis da empresa.
    /// Antes o valor existia na configuração mas não era editável e o aluguel usava 1000 fixo.
    /// </summary>
    public class ConfigurarValorGarantiaCommandHandler : IRequestHandler<ConfigurarValorGarantiaCommand, Result<ConfigurarValorGarantiaResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly IRepositorioConfiguracao _repositorioConfiguracao;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<ConfigurarValorGarantiaCommand> _validator;
        private readonly ILogger<ConfigurarValorGarantiaCommandHandler> _logger;

        public ConfigurarValorGarantiaCommandHandler(
            LocadoraDeVeiculosDbContext dbContext,
            IRepositorioConfiguracao repositorioConfiguracao,
            ITenantProvider tenantProvider,
            IValidator<ConfigurarValorGarantiaCommand> validator,
            ILogger<ConfigurarValorGarantiaCommandHandler> logger)
        {
            _dbContext = dbContext;
            _repositorioConfiguracao = repositorioConfiguracao;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<ConfigurarValorGarantiaResult>> Handle(
            ConfigurarValorGarantiaCommand command, CancellationToken cancellationToken)
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
                var configuracao = await _repositorioConfiguracao.SelecionarPorEmpresaIdAsync(empresaId);

                if (configuracao is null)
                {
                    configuracao = new Configuracao(empresaId, Configuracao.PrecoCombustivelPadrao)
                    {
                        ValorGarantia = command.ValorGarantia
                    };
                    await _repositorioConfiguracao.CadastrarAsync(configuracao);
                }
                else
                {
                    configuracao.ValorGarantia = command.ValorGarantia;
                    await _repositorioConfiguracao.EditarAsync(configuracao.Id, configuracao);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new ConfigurarValorGarantiaResult(
                    configuracao.Id,
                    configuracao.PrecoCombustivel,
                    configuracao.ValorGarantia));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a configuração do valor da garantia: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
