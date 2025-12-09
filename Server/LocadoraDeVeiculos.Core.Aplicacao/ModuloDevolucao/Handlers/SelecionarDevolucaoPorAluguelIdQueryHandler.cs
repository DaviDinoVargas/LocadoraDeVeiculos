using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Handlers
{
    public class SelecionarDevolucaoPorAluguelIdQueryHandler : IRequestHandler<SelecionarDevolucaoPorAluguelIdQuery, Result<SelecionarDevolucaoPorAluguelIdResult>>
    {
        private readonly IRepositorioDevolucao _repositorioDevolucao;

        public SelecionarDevolucaoPorAluguelIdQueryHandler(IRepositorioDevolucao repositorioDevolucao)
        {
            _repositorioDevolucao = repositorioDevolucao;
        }

        public async Task<Result<SelecionarDevolucaoPorAluguelIdResult>> Handle(
            SelecionarDevolucaoPorAluguelIdQuery query, CancellationToken cancellationToken)
        {
            var devolucao = await _repositorioDevolucao.SelecionarPorAluguelIdAsync(query.AluguelId);

            if (devolucao is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.AluguelId));

            var result = new SelecionarDevolucaoPorAluguelIdResult(
                devolucao.Id,
                devolucao.AluguelId,
                devolucao.DataDevolucao,
                devolucao.QuilometragemFinal,
                devolucao.CombustivelNoTanque,
                devolucao.NivelCombustivel.ToString(),
                devolucao.ValorMultas,
                devolucao.ValorAdicionalCombustivel,
                devolucao.ValorTotal
            );

            return Result.Ok(result);
        }
    }
}