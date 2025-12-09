using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Handlers
{
    public class CadastrarTaxaServicoCommandHandler : IRequestHandler<CadastrarTaxaServicoCommand, Result<CadastrarTaxaServicoResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarTaxaServicoCommand> _validator;
        private readonly ILogger<CadastrarTaxaServicoCommandHandler> _logger;

        public CadastrarTaxaServicoCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioTaxaServico repositorioTaxaServico,
            ITenantProvider tenantProvider,
            IValidator<CadastrarTaxaServicoCommand> validator,
            ILogger<CadastrarTaxaServicoCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioTaxaServico = repositorioTaxaServico;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarTaxaServicoResult>> Handle(
            CadastrarTaxaServicoCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            if (await _repositorioTaxaServico.ExisteTaxaServicoComNomeAsync(command.Nome))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe uma taxa/serviço com este nome."));
            }

            try
            {
                var taxaServico = new TaxaServico(
                    command.Nome,
                    command.Preco,
                    command.TipoCalculo
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioTaxaServico.CadastrarAsync(taxaServico);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarTaxaServicoResult(taxaServico.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de taxa/serviço: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}