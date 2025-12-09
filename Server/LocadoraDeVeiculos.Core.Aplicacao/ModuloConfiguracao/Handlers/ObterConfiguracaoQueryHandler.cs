using FluentResults;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloConfiguracao.Handlers
{
    public class ObterConfiguracaoQueryHandler : IRequestHandler<ObterConfiguracaoQuery, Result<ObterConfiguracaoResult>>
    {
        private readonly IRepositorioConfiguracao _repositorioConfiguracao;
        private readonly ITenantProvider _tenantProvider;

        public ObterConfiguracaoQueryHandler(
            IRepositorioConfiguracao repositorioConfiguracao,
            ITenantProvider tenantProvider)
        {
            _repositorioConfiguracao = repositorioConfiguracao;
            _tenantProvider = tenantProvider;
        }

        public async Task<Result<ObterConfiguracaoResult>> Handle(
            ObterConfiguracaoQuery query, CancellationToken cancellationToken)
        {
            var empresaId = _tenantProvider.EmpresaId.GetValueOrDefault();
            var configuracao = await _repositorioConfiguracao.SelecionarPorEmpresaIdAsync(empresaId);

            // Se não existir configuração, retornar valores padrão
            if (configuracao is null)
            {
                return Result.Ok(new ObterConfiguracaoResult(
                    Guid.Empty,
                    5.50m, // Valor padrão
                    1000m  // Valor da garantia conforme requisitos
                ));
            }

            return Result.Ok(new ObterConfiguracaoResult(
                configuracao.Id,
                configuracao.PrecoCombustivel,
                configuracao.ValorGarantia
            ));
        }
    }
}