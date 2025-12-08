using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Handlers
{
    public class SelecionarAutomovelPorIdQueryHandler : IRequestHandler<SelecionarAutomovelPorIdQuery, Result<SelecionarAutomovelPorIdResult>>
    {
        private readonly IRepositorioAutomovel _repositorioAutomovel;

        public SelecionarAutomovelPorIdQueryHandler(IRepositorioAutomovel repositorioAutomovel)
        {
            _repositorioAutomovel = repositorioAutomovel;
        }

        public async Task<Result<SelecionarAutomovelPorIdResult>> Handle(
            SelecionarAutomovelPorIdQuery query, CancellationToken cancellationToken)
        {
            var registroEncontrado = await _repositorioAutomovel.SelecionarPorIdAsync(query.Id);

            if (registroEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            var result = new SelecionarAutomovelPorIdResult(
                registroEncontrado.Id,
                registroEncontrado.Placa,
                registroEncontrado.Marca,
                registroEncontrado.Cor,
                registroEncontrado.Modelo,
                registroEncontrado.TipoCombustivel,
                registroEncontrado.CapacidadeTanque,
                registroEncontrado.Ano,
                registroEncontrado.Foto,
                registroEncontrado.GrupoAutomovelId,
                registroEncontrado.GrupoAutomovel?.Nome ?? string.Empty
            );

            return Result.Ok(result);
        }
    }
}