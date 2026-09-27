using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Handlers
{
    public class SelecionarDesafioCupomPorIdQueryHandler : IRequestHandler<SelecionarDesafioCupomPorIdQuery, Result<SelecionarDesafioCupomPorIdResult>>
    {
        private readonly IRepositorioDesafioCupom _repositorioDesafioCupom;

        public SelecionarDesafioCupomPorIdQueryHandler(IRepositorioDesafioCupom repositorioDesafioCupom)
        {
            _repositorioDesafioCupom = repositorioDesafioCupom;
        }

        public async Task<Result<SelecionarDesafioCupomPorIdResult>> Handle(
            SelecionarDesafioCupomPorIdQuery query, CancellationToken cancellationToken)
        {
            var desafio = await _repositorioDesafioCupom.SelecionarPorIdAsync(query.Id);

            if (desafio is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            return Result.Ok(new SelecionarDesafioCupomPorIdResult(
                desafio.Id, desafio.Nome, desafio.Descricao, desafio.MetaQuantidadeAlugueis, desafio.PeriodoDias,
                desafio.CupomRecompensaId, desafio.CupomRecompensa?.Codigo ?? string.Empty, desafio.Ativo));
        }
    }
}
