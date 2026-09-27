using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Handlers
{
    public class SelecionarParceiroPorIdQueryHandler : IRequestHandler<SelecionarParceiroPorIdQuery, Result<SelecionarParceiroPorIdResult>>
    {
        private readonly IRepositorioParceiro _repositorioParceiro;

        public SelecionarParceiroPorIdQueryHandler(IRepositorioParceiro repositorioParceiro)
        {
            _repositorioParceiro = repositorioParceiro;
        }

        public async Task<Result<SelecionarParceiroPorIdResult>> Handle(
            SelecionarParceiroPorIdQuery query, CancellationToken cancellationToken)
        {
            var parceiro = await _repositorioParceiro.SelecionarPorIdAsync(query.Id);

            if (parceiro is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(query.Id));

            return Result.Ok(new SelecionarParceiroPorIdResult(
                parceiro.Id, parceiro.Nome, parceiro.Cnpj, parceiro.Categoria, parceiro.Ativo));
        }
    }
}
