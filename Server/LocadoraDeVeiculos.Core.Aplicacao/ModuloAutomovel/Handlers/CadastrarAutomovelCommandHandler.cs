using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Handlers
{
    public class CadastrarAutomovelCommandHandler : IRequestHandler<CadastrarAutomovelCommand, Result<CadastrarAutomovelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioAutomovel _repositorioAutomovel;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarAutomovelCommand> _validator;
        private readonly ILogger<CadastrarAutomovelCommandHandler> _logger;

        public CadastrarAutomovelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioAutomovel repositorioAutomovel,
            ITenantProvider tenantProvider,
            IValidator<CadastrarAutomovelCommand> validator,
            ILogger<CadastrarAutomovelCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioAutomovel = repositorioAutomovel;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarAutomovelResult>> Handle(
            CadastrarAutomovelCommand command, CancellationToken cancellationToken)
        {
            // Validação do comando
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            // Verificar duplicidade de placa
            if (await _repositorioAutomovel.ExisteAutomovelComPlacaAsync(command.Placa))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um automóvel com esta placa."));
            }

            try
            {
                var automovel = new Automovel(
                    command.Placa,
                    command.Marca,
                    command.Cor,
                    command.Modelo,
                    command.TipoCombustivel,
                    command.CapacidadeTanque,
                    command.Ano,
                    command.Foto,
                    command.GrupoAutomovelId
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioAutomovel.CadastrarAsync(automovel);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarAutomovelResult(automovel.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}