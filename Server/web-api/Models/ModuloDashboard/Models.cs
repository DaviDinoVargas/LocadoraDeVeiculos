using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.WebApi.Models.ModuloDashboard
{
    public record ResumoDashboardResponse(
        ResumoFrotaResponse Frota,
        ResumoAlugueisResponse Alugueis,
        ResumoFinanceiroResponse Financeiro,
        int TotalClientes,
        IReadOnlyList<ResumoMesResponse> UltimosMeses,
        DateTimeOffset GeradoEm
    );

    public record ResumoFrotaResponse(int Total, int EmUso, int Disponiveis);

    public record ResumoAlugueisResponse(int EmAndamento, int Reservados, int Atrasados);

    public record ResumoFinanceiroResponse(decimal ReceitaDoMes, decimal ReceitaMesAnterior, int DevolucoesDoMes);

    public record ResumoMesResponse(int Ano, int Mes, int Alugueis, decimal Receita);
}
