using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloCupom.Handlers
{
    public class CadastrarCupomCommandHandler : IRequestHandler<CadastrarCupomCommand, Result<CadastrarCupomResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioCupom _repositorioCupom;
        private readonly IRepositorioParceiro _repositorioParceiro;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarCupomCommand> _validator;
        private readonly ILogger<CadastrarCupomCommandHandler> _logger;

        public CadastrarCupomCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioCupom repositorioCupom,
            IRepositorioParceiro repositorioParceiro,
            ITenantProvider tenantProvider,
            IValidator<CadastrarCupomCommand> validator,
            ILogger<CadastrarCupomCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioCupom = repositorioCupom;
            _repositorioParceiro = repositorioParceiro;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarCupomResult>> Handle(
            CadastrarCupomCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(resultadoValidacao.Errors.Select(e => e.ErrorMessage)));

            if (await _repositorioCupom.ExisteCupomComCodigoAsync(command.Codigo))
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um cupom com este código."));

            if (command.ParceiroId.HasValue && await _repositorioParceiro.SelecionarPorIdAsync(command.ParceiroId.Value) is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.ParceiroId.Value));

            try
            {
                var cupom = new Cupom(
                    command.Codigo,
                    command.Descricao,
                    command.TipoDesconto,
                    command.ValorDesconto,
                    command.ValidoAte,
                    command.LimiteUsos,
                    command.ParceiroId
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioCupom.CadastrarAsync(cupom);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarCupomResult(cupom.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de cupom: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
