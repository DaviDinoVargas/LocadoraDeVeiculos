using FluentResults;
using MediatR;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDashboard.Commands
{
    /// <summary>
    /// Números agregados da empresa (frota, aluguéis, faturamento) para a Home. Calculados no servidor:
    /// o front não precisa baixar todas as listas só para contar.
    /// </summary>
    public record ObterResumoDashboardQuery() : IRequest<Result<ResumoDashboardResult>>;

    public record ResumoDashboardResult(
        ResumoFrota Frota,
        ResumoAlugueis Alugueis,
        ResumoFinanceiro Financeiro,
        int TotalClientes,
        IReadOnlyList<ResumoMes> UltimosMeses,
        DateTimeOffset GeradoEm);

    /// <param name="Total">Automóveis cadastrados.</param>
    /// <param name="EmUso">Automóveis em aluguel já iniciado (EmAndamento).</param>
    /// <param name="Disponiveis">Total menos os indisponíveis agora (em uso, ou reservados cujo período cobre este momento).</param>
    public record ResumoFrota(int Total, int EmUso, int Disponiveis);

    /// <param name="Atrasados">Em andamento com a data de retorno previsto já vencida.</param>
    public record ResumoAlugueis(int EmAndamento, int Reservados, int Atrasados);

    /// <param name="ReceitaDoMes">Soma do valor total das devoluções registradas no mês corrente (UTC).</param>
    public record ResumoFinanceiro(decimal ReceitaDoMes, decimal ReceitaMesAnterior, int DevolucoesDoMes);

    /// <param name="Alugueis">Aluguéis não cancelados com saída no mês.</param>
    /// <param name="Receita">Soma das devoluções registradas no mês.</param>
    public record ResumoMes(int Ano, int Mes, int Alugueis, decimal Receita);
}
