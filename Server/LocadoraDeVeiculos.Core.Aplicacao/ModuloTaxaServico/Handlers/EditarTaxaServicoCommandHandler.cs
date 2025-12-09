using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands;
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
    public class EditarTaxaServicoCommandHandler : IRequestHandler<EditarTaxaServicoCommand, Result<EditarTaxaServicoResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;
        private readonly IValidator<EditarTaxaServicoCommand> _validator;
        private readonly ILogger<EditarTaxaServicoCommandHandler> _logger;

        public EditarTaxaServicoCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioTaxaServico repositorioTaxaServico,
            IValidator<EditarTaxaServicoCommand> validator,
            ILogger<EditarTaxaServicoCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioTaxaServico = repositorioTaxaServico;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarTaxaServicoResult>> Handle(
            EditarTaxaServicoCommand command, CancellationToken cancellationToken)
        {
            var taxaExistente = await _repositorioTaxaServico.SelecionarPorIdAsync(command.Id);

            if (taxaExistente is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            // Verificar se existe aluguel vinculado
            //if (await _repositorioTaxaServico.ExisteAluguelVinculadoAsync(command.Id))
            //    return Result.Fail(ResultadosErro.RegistroVinculadoErro("Não é possível editar uma taxa/serviço vinculada a um aluguel."));

            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            if (await _repositorioTaxaServico.ExisteTaxaServicoComNomeAsync(command.Nome, command.Id))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe uma taxa/serviço com este nome."));
            }

            try
            {
                var taxaServicoEditada = new TaxaServico(
                    command.Nome,
                    command.Preco,
                    command.TipoCalculo
                );

                await _repositorioTaxaServico.EditarAsync(command.Id, taxaServicoEditada);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarTaxaServicoResult(
                    command.Nome,
                    command.Preco,
                    command.TipoCalculo
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de taxa/serviço: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}