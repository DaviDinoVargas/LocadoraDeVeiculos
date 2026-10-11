using FluentResults;
using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Handlers
{
    public class SelecionarAutomoveisDisponiveisQueryHandler : IRequestHandler<SelecionarAutomoveisDisponiveisQuery, Result<SelecionarAutomoveisDisponiveisResult>>
    {
        private readonly IRepositorioAutomovel _repositorioAutomovel;
        private readonly IRepositorioAluguel _repositorioAluguel;
        private readonly IValidator<SelecionarAutomoveisDisponiveisQuery> _validator;

        public SelecionarAutomoveisDisponiveisQueryHandler(
            IRepositorioAutomovel repositorioAutomovel,
            IRepositorioAluguel repositorioAluguel,
            IValidator<SelecionarAutomoveisDisponiveisQuery> validator)
        {
            _repositorioAutomovel = repositorioAutomovel;
            _repositorioAluguel = repositorioAluguel;
            _validator = validator;
        }

        public async Task<Result<SelecionarAutomoveisDisponiveisResult>> Handle(
            SelecionarAutomoveisDisponiveisQuery query, CancellationToken cancellationToken)
        {
            var validacao = await _validator.ValidateAsync(query, cancellationToken);

            if (!validacao.IsValid)
            {
                var erros = validacao.Errors.Select(e => e.ErrorMessage);
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
            }

            var indisponiveis = (await _repositorioAluguel.SelecionarAutomoveisIndisponiveisAsync(
                query.DataSaida, query.DataRetornoPrevisto, query.AluguelIdExcluir)).ToHashSet();

            var automoveis = await _repositorioAutomovel.SelecionarTodosAsync();

            var disponiveis = automoveis
                .Where(a => !indisponiveis.Contains(a.Id))
                .Select(r => new SelecionarAutomoveisDto(
                    r.Id,
                    r.Placa,
                    r.Marca,
                    r.Cor,
                    r.Modelo,
                    r.TipoCombustivel,
                    r.CapacidadeTanque,
                    r.Ano,
                    r.Foto,
                    r.GrupoAutomovelId,
                    r.GrupoAutomovel?.Nome ?? string.Empty))
                .ToList();

            return Result.Ok(new SelecionarAutomoveisDisponiveisResult(disponiveis));
        }
    }
}
