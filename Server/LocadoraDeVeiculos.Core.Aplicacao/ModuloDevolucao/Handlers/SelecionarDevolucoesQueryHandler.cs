using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using MediatR;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Handlers
{
    public class SelecionarDevolucoesQueryHandler : IRequestHandler<SelecionarDevolucoesQuery, Result<SelecionarDevolucoesResult>>
    {
        private readonly IRepositorioDevolucao _repositorioDevolucao;

        public SelecionarDevolucoesQueryHandler(IRepositorioDevolucao repositorioDevolucao)
        {
            _repositorioDevolucao = repositorioDevolucao;
        }

        public async Task<Result<SelecionarDevolucoesResult>> Handle(
            SelecionarDevolucoesQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioDevolucao.SelecionarTodosAsync();

            var dtos = registros
                .Select(d => new SelecionarDevolucoesDto(
                    d.Id,
                    d.Aluguel?.Condutor?.Nome ?? "N/A",
                    d.Aluguel?.Automovel?.Placa ?? "N/A",
                    d.Aluguel?.Cliente?.Nome ?? "N/A",
                    d.DataDevolucao,
                    d.QuilometragemFinal,
                    d.ValorTotal
                ))
                .ToImmutableList();

            var response = new SelecionarDevolucoesResult(dtos);

            return Result.Ok(response);
        }
    }
}