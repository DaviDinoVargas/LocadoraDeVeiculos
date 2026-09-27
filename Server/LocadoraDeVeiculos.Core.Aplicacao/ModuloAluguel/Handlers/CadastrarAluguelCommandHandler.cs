using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Servicos;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers
{
    public class CadastrarAluguelCommandHandler : IRequestHandler<CadastrarAluguelCommand, Result<CadastrarAluguelResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly IRepositorioAutomovel _repositorioAutomovel;
        private readonly IRepositorioPlanoCobranca _repositorioPlanoCobranca;
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarAluguelCommand> _validator;
        private readonly ILogger<CadastrarAluguelCommandHandler> _logger;

        public CadastrarAluguelCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioAluguel repositorioAluguel,
            IRepositorioAutomovel repositorioAutomovel,
            IRepositorioPlanoCobranca repositorioPlanoCobranca,
            IRepositorioTaxaServico repositorioTaxaServico,
            ITenantProvider tenantProvider,
            IValidator<CadastrarAluguelCommand> validator,
            ILogger<CadastrarAluguelCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioAluguel = repositorioAluguel;
            _repositorioAutomovel = repositorioAutomovel;
            _repositorioPlanoCobranca = repositorioPlanoCobranca;
            _repositorioTaxaServico = repositorioTaxaServico;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarAluguelResult>> Handle(
            CadastrarAluguelCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            // Verificar disponibilidade do automóvel
            if (await _repositorioAluguel.ExisteAluguelAtivoParaAutomovelAsync(command.AutomovelId, command.DataSaida, command.DataRetornoPrevisto))
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

            try
            {
                var taxasServicos = command.TaxasServicosIds is { Count: > 0 }
                    ? await _repositorioTaxaServico.SelecionarPorIdsAsync(command.TaxasServicosIds)
                    : new System.Collections.Generic.List<TaxaServico>();

                // O valor previsto é sempre calculado no servidor a partir do plano de cobrança
                // e das taxas selecionadas — o valor enviado pelo cliente (command.ValorPrevisto)
                // é ignorado de propósito, nunca confiamos nele.
                var valorPrevisto = CalculoAluguelService.CalcularValorPrevisto(
                    planoCobranca, command.DataSaida, command.DataRetornoPrevisto, taxasServicos);

                var aluguel = new Aluguel(
                    command.CondutorId,
                    command.AutomovelId,
                    command.ClienteId,
                    command.DataSaida,
                    command.DataRetornoPrevisto,
                    valorPrevisto
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                aluguel.TaxasServicos.AddRange(taxasServicos);

                await _repositorioAluguel.CadastrarAsync(aluguel);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarAluguelResult(aluguel.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de aluguel: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}