using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Handlers
{
    public class SelecionarParceirosQueryHandler : IRequestHandler<SelecionarParceirosQuery, Result<SelecionarParceirosResult>>
    {
        private readonly IRepositorioParceiro _repositorioParceiro;

        public SelecionarParceirosQueryHandler(IRepositorioParceiro repositorioParceiro)
        {
            _repositorioParceiro = repositorioParceiro;
        }

        public async Task<Result<SelecionarParceirosResult>> Handle(
            SelecionarParceirosQuery query, CancellationToken cancellationToken)
        {
            var registros = await _repositorioParceiro.SelecionarTodosAsync();

            var dtos = registros
                .Select(p => new SelecionarParceirosDto(p.Id, p.Nome, p.Cnpj, p.Categoria, p.Ativo))
                .ToList();

            return Result.Ok(new SelecionarParceirosResult(dtos));
        }
    }
}
