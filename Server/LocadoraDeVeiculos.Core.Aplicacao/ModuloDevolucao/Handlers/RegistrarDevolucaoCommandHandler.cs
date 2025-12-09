using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Handlers
{
    public class RegistrarDevolucaoCommandHandler : IRequestHandler<RegistrarDevolucaoCommand, Result<RegistrarDevolucaoResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioDevolucao _repositorioDevolucao;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<RegistrarDevolucaoCommand> _validator;
        private readonly ILogger<RegistrarDevolucaoCommandHandler> _logger;
        private readonly decimal _precoCombustivel = 5.50m; // TODO: Buscar do módulo de Configurações! NÃO ESQUECER

        public RegistrarDevolucaoCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioDevolucao repositorioDevolucao,
            IRepositorioAluguel repositorioAluguel,
            ITenantProvider tenantProvider,
            IValidator<RegistrarDevolucaoCommand> validator,
            ILogger<RegistrarDevolucaoCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioDevolucao = repositorioDevolucao;
            _repositorioAluguel = repositorioAluguel;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<RegistrarDevolucaoResult>> Handle(
            RegistrarDevolucaoCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
            {
                var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            // Verificar se o aluguel existe e está em andamento
            var aluguel = await _repositorioAluguel.SelecionarPorIdAsync(command.AluguelId);

            if (aluguel is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.AluguelId));

            //if (aluguel.Status != StatusAluguel.EmAndamento)
            //    return Result.Fail(ResultadosErro.RegistroInvalidoErro("Só é possível registrar devolução para aluguéis em andamento."));

            // Verificar se já existe devolução para este aluguel
            if (await _repositorioDevolucao.ExisteDevolucaoParaAluguelAsync(command.AluguelId))
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe uma devolução registrada para este aluguel."));

            try
            {
                var devolucao = new Devolucao(
                    command.AluguelId,
                    command.DataDevolucao,
                    command.QuilometragemFinal,
                    command.CombustivelNoTanque,
                    command.NivelCombustivel
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                // Calcular valores (multas, combustível, total)
                // TODO: Buscar preço do combustível do módulo de Configurações
                // TODO: Buscar capacidade do tanque do automóvel
                decimal capacidadeTanque = aluguel.Automovel?.CapacidadeTanque ?? 50m;
                decimal quilometragemInicial = 0; // TODO: Adicionar quilometragem inicial no aluguel

                devolucao.CalcularValores(
                    _precoCombustivel,
                    capacidadeTanque,
                    quilometragemInicial,
                    aluguel.DataRetornoPrevisto,
                    aluguel.ValorPrevisto
                );

                await _repositorioDevolucao.CadastrarAsync(devolucao);

                // Atualizar status do aluguel para Concluído
                aluguel.Status = StatusAluguel.Concluido;
                await _repositorioAluguel.EditarAsync(aluguel.Id, aluguel);

                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new RegistrarDevolucaoResult(
                    devolucao.Id,
                    devolucao.AluguelId,
                    devolucao.DataDevolucao,
                    devolucao.QuilometragemFinal,
                    devolucao.CombustivelNoTanque,
                    devolucao.ValorMultas,
                    devolucao.ValorAdicionalCombustivel,
                    devolucao.ValorTotal
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o registro de devolução: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}