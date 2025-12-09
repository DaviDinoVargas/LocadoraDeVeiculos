using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Handlers
{
    public class SelecionarAluguelPorIdQueryHandler : IRequestHandler<SelecionarAluguelPorIdQuery, Result<SelecionarAluguelPorIdResult>>
    {
        private readonly IRepositorioAluguel _repositorioAluguel;

        public SelecionarAluguelPorIdQueryHandler(IRepositorioAluguel repositorioAluguel)
        {
            _repositorioAluguel = repositorioAluguel;
        }

        public async Task<Result<SelecionarAluguelPorIdResult>> Handle(
            SelecionarAluguelPorIdQuery query, CancellationToken cancellationToken)
        {
            var aluguel = await _repositorioAluguel.SelecionarAluguelCompletoPorIdAsync(query.Id);

            if (aluguel is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            var taxasServicos = aluguel.TaxasServicos
                .Select(t => new SelecionarTaxaServicoDto(
                    t.Id,
                    t.Nome,
                    t.Preco,
                    t.TipoCalculo.ToString()
                ))
                .ToList();

            var result = new SelecionarAluguelPorIdResult(
                aluguel.Id,
                aluguel.CondutorId,
                aluguel.Condutor?.Nome ?? "N/A",
                aluguel.AutomovelId,
                aluguel.Automovel?.Placa ?? "N/A",
                aluguel.ClienteId,
                aluguel.Cliente?.Nome ?? "N/A",
                aluguel.DataSaida,
                aluguel.DataRetornoPrevisto,
                aluguel.ValorPrevisto,
                aluguel.ValorCaucao,
                aluguel.Status.ToString(),
                taxasServicos
            );

            return Result.Ok(result);
        }
    }
}