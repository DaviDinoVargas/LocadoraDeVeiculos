using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Handlers
{
    public class ValidarCupomQueryHandler : IRequestHandler<ValidarCupomQuery, Result<ValidarCupomResult>>
    {
        private readonly IRepositorioCupom _repositorioCupom;

        public ValidarCupomQueryHandler(IRepositorioCupom repositorioCupom)
        {
            _repositorioCupom = repositorioCupom;
        }

        public async Task<Result<ValidarCupomResult>> Handle(
            ValidarCupomQuery query, CancellationToken cancellationToken)
        {
            var cupom = await _repositorioCupom.SelecionarPorCodigoAsync(query.Codigo);

            if (cupom is null)
                return Result.Ok(new ValidarCupomResult(false, "Cupom não encontrado.", 0, query.ValorBase));

            if (!cupom.EstaValido(DateTimeOffset.UtcNow))
            {
                var motivo = !cupom.Ativo
                    ? "Cupom inativo."
                    : cupom.ValidoAte < DateTimeOffset.UtcNow
                        ? "Cupom expirado."
                        : "Cupom esgotou o limite de usos.";

                return Result.Ok(new ValidarCupomResult(false, motivo, 0, query.ValorBase));
            }

            var desconto = cupom.CalcularDesconto(query.ValorBase);

            return Result.Ok(new ValidarCupomResult(true, null, desconto, query.ValorBase - desconto));
        }
    }
}
