using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Handlers
{
    public class SelecionarDevolucaoPorIdQueryHandler : IRequestHandler<SelecionarDevolucaoPorIdQuery, Result<SelecionarDevolucaoPorIdResult>>
    {
        private readonly IRepositorioDevolucao _repositorioDevolucao;

        public SelecionarDevolucaoPorIdQueryHandler(IRepositorioDevolucao repositorioDevolucao)
        {
            _repositorioDevolucao = repositorioDevolucao;
        }

        public async Task<Result<SelecionarDevolucaoPorIdResult>> Handle(
            SelecionarDevolucaoPorIdQuery query, CancellationToken cancellationToken)
        {
            var devolucao = await _repositorioDevolucao.SelecionarPorIdAsync(query.Id);

            if (devolucao is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            var result = new SelecionarDevolucaoPorIdResult(
                devolucao.Id,
                devolucao.AluguelId,
                devolucao.Aluguel?.Condutor?.Nome ?? "N/A",
                devolucao.Aluguel?.Automovel?.Placa ?? "N/A",
                devolucao.Aluguel?.Cliente?.Nome ?? "N/A",
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