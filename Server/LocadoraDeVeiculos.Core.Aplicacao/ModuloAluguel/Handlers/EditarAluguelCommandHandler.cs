using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers
{
    public class EditarAluguelCommandHandler : IRequestHandler<EditarAluguelCommand, Result<EditarAluguelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;
        private readonly IValidator<EditarAluguelCommand> _validator;
        private readonly ILogger<EditarAluguelCommandHandler> _logger;

        public EditarAluguelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioAluguel repositorioAluguel,
            IRepositorioTaxaServico repositorioTaxaServico,
            IValidator<EditarAluguelCommand> validator,
            ILogger<EditarAluguelCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioAluguel = repositorioAluguel;
            _repositorioTaxaServico = repositorioTaxaServico;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarAluguelResult>> Handle(
            EditarAluguelCommand command, CancellationToken cancellationToken)
        {
            var aluguelExistente = await _repositorioAluguel.SelecionarAluguelCompletoPorIdAsync(command.Id);

            if (aluguelExistente is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            //if (!aluguelExistente.PodeSerEditado())
            //    return Result.Fail(ResultadosErro.RegistroInvalidoErro("Não é possível editar um aluguel concluído ou cancelado."));

            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            // Verificar disponibilidade do automóvel (excluindo o próprio aluguel)
            if (await _repositorioAluguel.ExisteAluguelAtivoParaAutomovelAsync(command.AutomovelId, command.DataSaida, command.DataRetornoPrevisto, command.Id))
            {
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("O automóvel já está reservado para este período."));
            }

            // Verificar documentos do condutor
            //if (!await _repositorioAluguel.VerificarDocumentosCondutorEmDiaAsync(command.CondutorId))
            //{
            //    return Result.Fail(ResultadosErro.RegistroInvalidoErro("Os documentos do condutor não estão em dia."));
            //}

            try
            {
                var aluguelEditado = new Aluguel(
                    command.CondutorId,
                    command.AutomovelId,
                    command.ClienteId,
                    command.DataSaida,
                    command.DataRetornoPrevisto,
                    command.ValorPrevisto
                )
                {
                    Status = aluguelExistente.Status
                };

                // Atualizar taxas e serviços
                aluguelEditado.TaxasServicos.Clear();
                if (command.TaxasServicosIds != null && command.TaxasServicosIds.Any())
                {
                    var taxasServicos = await _repositorioTaxaServico.SelecionarPorIdsAsync(command.TaxasServicosIds);
                    aluguelEditado.TaxasServicos.AddRange(taxasServicos);
                }

                await _repositorioAluguel.EditarAsync(command.Id, aluguelEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarAluguelResult(
                    command.Id,
                    command.CondutorId,
                    command.AutomovelId,
                    command.ClienteId,
                    command.DataSaida,
                    command.DataRetornoPrevisto,
                    command.ValorPrevisto
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de aluguel: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}