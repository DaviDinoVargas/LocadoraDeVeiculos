using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDashboard.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDashboard.Handlers
{
    public class ObterResumoDashboardQueryHandler : IRequestHandler<ObterResumoDashboardQuery, Result<ResumoDashboardResult>>
    {
        private const int MesesNoGrafico = 6;

        private readonly LocadoraDeVeiculosDbContext _dbContext;
        private readonly TimeProvider _relogio;
        private readonly ILogger<ObterResumoDashboardQueryHandler> _logger;

        public ObterResumoDashboardQueryHandler(
            LocadoraDeVeiculosDbContext dbContext,
            TimeProvider relogio,
            ILogger<ObterResumoDashboardQueryHandler> logger)
        {
            _dbContext = dbContext;
            _relogio = relogio;
            _logger = logger;
        }

        public async Task<Result<ResumoDashboardResult>> Handle(
            ObterResumoDashboardQuery query, CancellationToken cancellationToken)
        {
            try
            {
                // Todas as consultas passam pelo filtro multi-tenant do DbContext: cada empresa só soma os próprios dados.
                var agora = _relogio.GetUtcNow();
                var inicioMes = new DateTimeOffset(agora.Year, agora.Month, 1, 0, 0, 0, TimeSpan.Zero);
                var inicioMesSeguinte = inicioMes.AddMonths(1);
                var inicioJanela = inicioMes.AddMonths(-(MesesNoGrafico - 1));

                // ---- Frota
                var totalAutomoveis = await _dbContext.Automoveis.CountAsync(cancellationToken);

                var emUso = await _dbContext.Alugueis
                    .Where(a => a.Status == StatusAluguel.EmAndamento)
                    .Select(a => a.AutomovelId).Distinct()
                    .CountAsync(cancellationToken);

                var indisponiveisAgora = await _dbContext.Alugueis
                    .Where(a => a.Status == StatusAluguel.EmAndamento ||
                               (a.Status == StatusAluguel.Reservado && a.DataSaida <= agora && a.DataRetornoPrevisto >= agora))
                    .Select(a => a.AutomovelId).Distinct()
                    .CountAsync(cancellationToken);

                // ---- Aluguéis
                var emAndamento = await _dbContext.Alugueis.CountAsync(a => a.Status == StatusAluguel.EmAndamento, cancellationToken);
                var reservados = await _dbContext.Alugueis.CountAsync(a => a.Status == StatusAluguel.Reservado, cancellationToken);
                var atrasados = await _dbContext.Alugueis.CountAsync(
                    a => a.Status == StatusAluguel.EmAndamento && a.DataRetornoPrevisto < agora, cancellationToken);

                // ---- Clientes
                var totalClientes = await _dbContext.Clientes.CountAsync(cancellationToken);

                // ---- Janela dos últimos meses (agrupada em memória: poucos registros e evita depender de
                // tradução de DateTimeOffset para GROUP BY em cada provider)
                var devolucoes = await _dbContext.Devolucoes
                    .Where(d => d.DataDevolucao >= inicioJanela && d.DataDevolucao < inicioMesSeguinte)
                    .Select(d => new { d.DataDevolucao, d.ValorTotal })
                    .ToListAsync(cancellationToken);

                var saidas = await _dbContext.Alugueis
                    .Where(a => a.Status != StatusAluguel.Cancelado && a.DataSaida >= inicioJanela && a.DataSaida < inicioMesSeguinte)
                    .Select(a => a.DataSaida)
                    .ToListAsync(cancellationToken);

                var meses = new List<ResumoMes>();
                for (var i = 0; i < MesesNoGrafico; i++)
                {
                    var inicio = inicioJanela.AddMonths(i);
                    var fim = inicio.AddMonths(1);

                    meses.Add(new ResumoMes(
                        inicio.Year,
                        inicio.Month,
                        saidas.Count(s => s >= inicio && s < fim),
                        devolucoes.Where(d => d.DataDevolucao >= inicio && d.DataDevolucao < fim).Sum(d => d.ValorTotal)));
                }

                var mesCorrente = meses[^1];
                var mesAnterior = meses[^2];
                var devolucoesDoMes = devolucoes.Count(d => d.DataDevolucao >= inicioMes);

                return Result.Ok(new ResumoDashboardResult(
                    new ResumoFrota(totalAutomoveis, emUso, Math.Max(0, totalAutomoveis - indisponiveisAgora)),
                    new ResumoAlugueis(emAndamento, reservados, atrasados),
                    new ResumoFinanceiro(mesCorrente.Receita, mesAnterior.Receita, devolucoesDoMes),
                    totalClientes,
                    meses,
                    agora));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro ao montar o resumo do dashboard.");
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
