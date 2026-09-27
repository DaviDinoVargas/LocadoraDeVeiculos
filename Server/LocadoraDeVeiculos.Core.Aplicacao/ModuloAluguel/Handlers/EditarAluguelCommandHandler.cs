using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Servicos;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
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
        private readonly IRepositorioAutomovel _repositorioAutomovel;
        private readonly IRepositorioPlanoCobranca _repositorioPlanoCobranca;
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;
        private readonly IRepositorioCupom _repositorioCupom;
        private readonly IValidator<EditarAluguelCommand> _validator;
        private readonly ILogger<EditarAluguelCommandHandler> _logger;

        public EditarAluguelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioAluguel repositorioAluguel,
            IRepositorioAutomovel repositorioAutomovel,
            IRepositorioPlanoCobranca repositorioPlanoCobranca,
            IRepositorioTaxaServico repositorioTaxaServico,
            IRepositorioCupom repositorioCupom,
            IValidator<EditarAluguelCommand> validator,
            ILogger<EditarAluguelCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioAluguel = repositorioAluguel;
            _repositorioAutomovel = repositorioAutomovel;
            _repositorioPlanoCobranca = repositorioPlanoCobranca;
            _repositorioTaxaServico = repositorioTaxaServico;
            _repositorioCupom = repositorioCupom;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarAluguelResult>> Handle(
            EditarAluguelCommand command, CancellationToken cancellationToken)
        {
            var aluguelExistente = await _repositorioAluguel.SelecionarAluguelCompletoPorIdAsync(command.Id);

            if (aluguelExistente is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            if (!aluguelExistente.PodeSerEditado())
                return Result.Fail(ResultadosErro.EstadoInvalidoErro("Não é possível editar um aluguel concluído ou cancelado."));

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

            if (!await _repositorioAluguel.VerificarDocumentosCondutorEmDiaAsync(command.CondutorId))
            {
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro("Os documentos do condutor não estão em dia (CNH vencida)."));
            }

            var automovel = await _repositorioAutomovel.SelecionarPorIdAsync(command.AutomovelId);

            if (automovel is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.AutomovelId));

            var planoCobranca = await _repositorioPlanoCobranca.SelecionarMaisRecentePorGrupoAutomovelAsync(automovel.GrupoAutomovelId);

            if (planoCobranca is null)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                    "Não há plano de cobrança cadastrado para o grupo deste automóvel."));

            Cupom? cupom = null;

            if (!string.IsNullOrWhiteSpace(command.CupomCodigo))
            {
                cupom = await _repositorioCupom.SelecionarPorCodigoAsync(command.CupomCodigo);

                if (cupom is null || !cupom.EstaValido(DateTimeOffset.UtcNow))
                    return Result.Fail(ResultadosErro.RequisicaoInvalidaErro("Cupom inválido, expirado ou esgotado."));
            }

            try
            {
                var taxasServicos = command.TaxasServicosIds is { Count: > 0 }
                    ? await _repositorioTaxaServico.SelecionarPorIdsAsync(command.TaxasServicosIds)
                    : new System.Collections.Generic.List<TaxaServico>();

                // O valor previsto é sempre recalculado no servidor — o valor enviado pelo
                // cliente (command.ValorPrevisto) é ignorado de propósito.
                var valorBruto = CalculoAluguelService.CalcularValorPrevisto(
                    planoCobranca, command.DataSaida, command.DataRetornoPrevisto, taxasServicos);

                var aluguelEditado = new Aluguel(
                    command.CondutorId,
                    command.AutomovelId,
                    command.ClienteId,
                    command.DataSaida,
                    command.DataRetornoPrevisto,
                    valorBruto
                )
                {
                    Status = aluguelExistente.Status
                };

                aluguelEditado.TaxasServicos.Clear();
                aluguelEditado.TaxasServicos.AddRange(taxasServicos);

                if (cupom is not null)
                {
                    aluguelEditado.AplicarCupom(cupom, valorBruto);

                    // Só registra um novo uso se for um cupom diferente do que já estava
                    // aplicado — evita contar uso duplicado ao simplesmente re-salvar o mesmo aluguel.
                    if (aluguelExistente.CupomId != cupom.Id)
                        cupom.RegistrarUso();
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
                    aluguelEditado.ValorPrevisto,
                    aluguelEditado.ValorDesconto
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