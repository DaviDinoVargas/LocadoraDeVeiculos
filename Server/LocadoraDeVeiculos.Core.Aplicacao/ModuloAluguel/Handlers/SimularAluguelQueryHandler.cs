using FluentResults;
using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Servicos;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers
{
    public class SimularAluguelQueryHandler : IRequestHandler<SimularAluguelQuery, Result<SimularAluguelResult>>
    {
        private readonly IRepositorioAutomovel _repositorioAutomovel;
        private readonly IRepositorioPlanoCobranca _repositorioPlanoCobranca;
        private readonly IRepositorioTaxaServico _repositorioTaxaServico;
        private readonly IRepositorioConfiguracao _repositorioConfiguracao;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<SimularAluguelQuery> _validator;
        private readonly ILogger<SimularAluguelQueryHandler> _logger;

        public SimularAluguelQueryHandler(
            IRepositorioAutomovel repositorioAutomovel,
            IRepositorioPlanoCobranca repositorioPlanoCobranca,
            IRepositorioTaxaServico repositorioTaxaServico,
            IRepositorioConfiguracao repositorioConfiguracao,
            ITenantProvider tenantProvider,
            IValidator<SimularAluguelQuery> validator,
            ILogger<SimularAluguelQueryHandler> logger)
        {
            _repositorioAutomovel = repositorioAutomovel;
            _repositorioPlanoCobranca = repositorioPlanoCobranca;
            _repositorioTaxaServico = repositorioTaxaServico;
            _repositorioConfiguracao = repositorioConfiguracao;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<SimularAluguelResult>> Handle(
            SimularAluguelQuery query, CancellationToken cancellationToken)
        {
            var resultadoValidacao = await _validator.ValidateAsync(query, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            try
            {
                var automovel = await _repositorioAutomovel.SelecionarPorIdAsync(query.AutomovelId);

                if (automovel is null)
                    return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.AutomovelId));

                var plano = await _repositorioPlanoCobranca.SelecionarMaisRecentePorGrupoAutomovelAsync(automovel.GrupoAutomovelId);

                if (plano is null)
                    return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(
                        "Não há plano de cobrança cadastrado para o grupo deste automóvel."));

                var taxas = query.TaxasServicosIds is { Count: > 0 }
                    ? await _repositorioTaxaServico.SelecionarPorIdsAsync(query.TaxasServicosIds)
                    : new List<TaxaServico>();

                var detalhe = CalculoAluguelService.Detalhar(plano, query.DataSaida, query.DataRetornoPrevisto, taxas);

                var configuracao = await _repositorioConfiguracao.SelecionarPorEmpresaIdAsync(_tenantProvider.EmpresaId.GetValueOrDefault());
                var caucao = configuracao?.ValorGarantia ?? Configuracao.ValorGarantiaPadrao;

                return Result.Ok(new SimularAluguelResult(
                    detalhe.Dias,
                    plano.Nome,
                    detalhe.PrecoDiaria,
                    detalhe.ValorDiarias,
                    detalhe.ValorTaxas,
                    detalhe.ValorPrevisto,
                    caucao));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a simulação do aluguel: {@Query}.", query);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
