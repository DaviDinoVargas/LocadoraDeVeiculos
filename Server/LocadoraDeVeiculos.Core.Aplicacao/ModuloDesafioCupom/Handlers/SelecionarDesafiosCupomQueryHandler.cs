using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Handlers
{
    public class SelecionarDesafiosCupomQueryHandler : IRequestHandler<SelecionarDesafiosCupomQuery, Result<SelecionarDesafiosCupomResult>>
    {
        private readonly IRepositorioDesafioCupom _repositorioDesafioCupom;

        public SelecionarDesafiosCupomQueryHandler(IRepositorioDesafioCupom repositorioDesafioCupom)
        {
            _repositorioDesafioCupom = repositorioDesafioCupom;
        }

        public async Task<Result<SelecionarDesafiosCupomResult>> Handle(
            SelecionarDesafiosCupomQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioDesafioCupom.SelecionarTodosAsync();

            var dtos = registros
                .Select(d => new SelecionarDesafiosCupomDto(
                    d.Id, d.Nome, d.Descricao, d.MetaQuantidadeAlugueis, d.PeriodoDias,
                    d.CupomRecompensa?.Codigo ?? string.Empty, d.Ativo))
                .ToList();

            return Result.Ok(new SelecionarDesafiosCupomResult(dtos));
        }
    }
}
