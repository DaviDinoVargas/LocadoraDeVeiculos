using LocadoraDeVeiculos.Core.Aplicacao.ModuloDashboard.Commands;
using LocadoraDeVeiculos.WebApi.Compartilhado;
using LocadoraDeVeiculos.WebApi.Models.ModuloDashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.WebApi.Controllers
{
    [Authorize(Roles = "Empresa,Funcionario")]
    [Route("api/dashboard")]
    public sealed class DashboardController(IMediator mediator) : MainController
    {
        /// <summary>Números agregados para a Home (frota, aluguéis, faturamento e últimos meses).</summary>
        [HttpGet("resumo")]
        public async Task<ActionResult<ResumoDashboardResponse>> ObterResumo(CancellationToken cancellationToken)
        {
            var result = await mediator.Send(new ObterResumoDashboardQuery(), cancellationToken);

            return ProcessarResultado(result, (valor) => Ok(new ResumoDashboardResponse(
                new ResumoFrotaResponse(valor.Frota.Total, valor.Frota.EmUso, valor.Frota.Disponiveis),
                new ResumoAlugueisResponse(valor.Alugueis.EmAndamento, valor.Alugueis.Reservados, valor.Alugueis.Atrasados),
                new ResumoFinanceiroResponse(valor.Financeiro.ReceitaDoMes, valor.Financeiro.ReceitaMesAnterior, valor.Financeiro.DevolucoesDoMes),
                valor.TotalClientes,
                valor.UltimosMeses.Select(m => new ResumoMesResponse(m.Ano, m.Mes, m.Alugueis, m.Receita)).ToList(),
                valor.GeradoEm)));
        }
    }
}
