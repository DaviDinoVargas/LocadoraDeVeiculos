using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Handlers
{
    public class SelecionarCupomPorIdQueryHandler : IRequestHandler<SelecionarCupomPorIdQuery, Result<SelecionarCupomPorIdResult>>
    {
        private readonly IRepositorioCupom _repositorioCupom;

        public SelecionarCupomPorIdQueryHandler(IRepositorioCupom repositorioCupom)
        {
            _repositorioCupom = repositorioCupom;
        }

        public async Task<Result<SelecionarCupomPorIdResult>> Handle(
            SelecionarCupomPorIdQuery query, CancellationToken cancellationToken)
        {
            var cupom = await _repositorioCupom.SelecionarPorIdAsync(query.Id);

            if (cupom is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            return Result.Ok(new SelecionarCupomPorIdResult(
                cupom.Id, cupom.Codigo, cupom.Descricao, cupom.TipoDesconto, cupom.ValorDesconto,
                cupom.ValidoAte, cupom.LimiteUsos, cupom.UsosAtuais, cupom.ParceiroId, cupom.Parceiro?.Nome, cupom.Ativo));
        }
    }
}
