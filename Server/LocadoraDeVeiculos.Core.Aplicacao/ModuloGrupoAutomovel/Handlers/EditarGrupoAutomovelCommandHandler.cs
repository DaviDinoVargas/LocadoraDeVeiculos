using FluentResults;
using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Handlers
{
    public class EditarGrupoAutomovelCommandHandler
        : IRequestHandler<EditarGrupoAutomovelCommand, Result<EditarGrupoAutomovelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioGrupoAutomovel _repositorioGrupoAutomovel;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<EditarGrupoAutomovelCommand> _validator;
        private readonly ILogger<EditarGrupoAutomovelCommandHandler> _logger;

        public EditarGrupoAutomovelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioGrupoAutomovel repositorioGrupoAutomovel,
            ITenantProvider tenantProvider,
            IValidator<EditarGrupoAutomovelCommand> validator,
            ILogger<EditarGrupoAutomovelCommandHandler> logger
        )
        {
            _appDbContext = appDbContext;
            _repositorioGrupoAutomovel = repositorioGrupoAutomovel;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarGrupoAutomovelResult>> Handle(
            EditarGrupoAutomovelCommand command, CancellationToken cancellationToken)
        {
            // Buscar registro existente
            var registroEncontrado = await _repositorioGrupoAutomovel.SelecionarPorIdAsync(command.Id);

            if (registroEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            // Validação do comando
            var resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            //// Verificar duplicidade (excluindo o próprio registro)
            //if (await _repositorioGrupoAutomovel.ExisteGrupoComNomeAsync(command.Nome, command.Id))
            //{
            //    return Result.Fail(ResultadosErro.RegistroDuplicadoErro(
            //        "Um grupo de automóvel com este nome já está cadastrado."));
            //}

            try
            {
                var grupoAutomovelEditado = new GrupoAutomovel(
                    command.Nome,
                    command.Descricao
                );

                await _repositorioGrupoAutomovel.EditarAsync(command.Id, grupoAutomovelEditado);

                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarGrupoAutomovelResult(
                    command.Nome,
                    command.Descricao
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
