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
    public class EditarAutomovelCommandHandler : IRequestHandler<EditarAutomovelCommand, Result<EditarAutomovelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioAutomovel _repositorioAutomovel;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<EditarAutomovelCommand> _validator;
        private readonly ILogger<EditarAutomovelCommandHandler> _logger;

        public EditarAutomovelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioAutomovel repositorioAutomovel,
            ITenantProvider tenantProvider,
            IValidator<EditarAutomovelCommand> validator,
            ILogger<EditarAutomovelCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioAutomovel = repositorioAutomovel;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarAutomovelResult>> Handle(
            EditarAutomovelCommand command, CancellationToken cancellationToken)
        {
            var registroEncontrado = await _repositorioAutomovel.SelecionarPorIdAsync(command.Id);

            if (registroEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            // Verificar se existe aluguel em aberto para este automóvel
            if (await _repositorioAutomovel.ExisteAluguelEmAbertoAsync(command.Id))
                return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível editar um automóvel com aluguel em aberto."));

            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            // Verificar duplicidade de placa (excluindo o próprio)
            if (await _repositorioAutomovel.ExisteAutomovelComPlacaAsync(command.Placa, command.Id))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um automóvel com esta placa."));
            }

            try
            {
                var automovelEditado = new Automovel(
                    command.Placa,
                    command.Marca,
                    command.Cor,
                    command.Modelo,
                    command.TipoCombustivel,
                    command.CapacidadeTanque,
                    command.Ano,
                    command.Foto,
                    command.GrupoAutomovelId
                );

                await _repositorioAutomovel.EditarAsync(command.Id, automovelEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarAutomovelResult(
                    command.Placa,
                    command.Marca,
                    command.Cor,
                    command.Modelo,
                    command.TipoCombustivel,
                    command.CapacidadeTanque,
                    command.Ano,
                    command.Foto,
                    command.GrupoAutomovelId
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}