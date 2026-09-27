using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Handlers
{
    public class VerificarDesafiosClienteQueryHandler : IRequestHandler<VerificarDesafiosClienteQuery, Result<VerificarDesafiosClienteResult>>
    {
        private readonly IRepositorioDesafioCupom _repositorioDesafioCupom;

        public VerificarDesafiosClienteQueryHandler(IRepositorioDesafioCupom repositorioDesafioCupom)
        {
            _repositorioDesafioCupom = repositorioDesafioCupom;
        }

        public async Task<Result<VerificarDesafiosClienteResult>> Handle(
            VerificarDesafiosClienteQuery query, CancellationToken cancellationToken)
        {
            var desafiosAtivos = (await _repositorioDesafioCupom.SelecionarTodosAsync())
                .Where(d => d.Ativo)
                .ToList();

            var progresso = new List<ProgressoDesafioDto>();

            foreach (var desafio in desafiosAtivos)
            {
                var quantidade = await _repositorioDesafioCupom.ContarAlugueisConcluidosDoClienteAsync(
                    query.ClienteId, desafio.PeriodoDias);

                var cumprido = quantidade >= desafio.MetaQuantidadeAlugueis;

                progresso.Add(new ProgressoDesafioDto(
                    desafio.Id,
                    desafio.Nome,
                    desafio.MetaQuantidadeAlugueis,
                    quantidade,
                    cumprido,
                    cumprido ? desafio.CupomRecompensa?.Codigo : null));
            }

            return Result.Ok(new VerificarDesafiosClienteResult(progresso));
        }
    }
}
