using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloDesafioCupom.Handlers
{
    public class CadastrarDesafioCupomCommandHandler : IRequestHandler<CadastrarDesafioCupomCommand, Result<CadastrarDesafioCupomResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioDesafioCupom _repositorioDesafioCupom;
        private readonly IRepositorioCupom _repositorioCupom;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarDesafioCupomCommand> _validator;
        private readonly ILogger<CadastrarDesafioCupomCommandHandler> _logger;

        public CadastrarDesafioCupomCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioDesafioCupom repositorioDesafioCupom,
            IRepositorioCupom repositorioCupom,
            ITenantProvider tenantProvider,
            IValidator<CadastrarDesafioCupomCommand> validator,
            ILogger<CadastrarDesafioCupomCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioDesafioCupom = repositorioDesafioCupom;
            _repositorioCupom = repositorioCupom;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarDesafioCupomResult>> Handle(
            CadastrarDesafioCupomCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(resultadoValidacao.Errors.Select(e => e.ErrorMessage)));

            if (await _repositorioCupom.SelecionarPorIdAsync(command.CupomRecompensaId) is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.CupomRecompensaId));

            try
            {
                var desafio = new DesafioCupom(
                    command.Nome,
                    command.Descricao,
                    command.MetaQuantidadeAlugueis,
                    command.PeriodoDias,
                    command.CupomRecompensaId
                )
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioDesafioCupom.CadastrarAsync(desafio);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarDesafioCupomResult(desafio.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de desafio de cupom: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
