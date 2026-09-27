using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Handlers
{
    public class SelecionarCuponsQueryHandler : IRequestHandler<SelecionarCuponsQuery, Result<SelecionarCuponsResult>>
    {
        private readonly IRepositorioCupom _repositorioCupom;

        public SelecionarCuponsQueryHandler(IRepositorioCupom repositorioCupom)
        {
            _repositorioCupom = repositorioCupom;
        }

        public async Task<Result<SelecionarCuponsResult>> Handle(
            SelecionarCuponsQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioCupom.SelecionarTodosAsync();

            var dtos = registros
                .Select(c => new SelecionarCuponsDto(
                    c.Id, c.Codigo, c.Descricao, c.TipoDesconto, c.ValorDesconto,
                    c.ValidoAte, c.LimiteUsos, c.UsosAtuais, c.Ativo))
                .ToList();

            return Result.Ok(new SelecionarCuponsResult(dtos));
        }
    }
}
