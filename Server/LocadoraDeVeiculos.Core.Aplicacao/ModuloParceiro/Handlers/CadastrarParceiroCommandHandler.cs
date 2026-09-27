using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Handlers
{
    public class CadastrarParceiroCommandHandler : IRequestHandler<CadastrarParceiroCommand, Result<CadastrarParceiroResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioParceiro _repositorioParceiro;
        private readonly ITenantProvider _tenantProvider;
        private readonly IValidator<CadastrarParceiroCommand> _validator;
        private readonly ILogger<CadastrarParceiroCommandHandler> _logger;

        public CadastrarParceiroCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioParceiro repositorioParceiro,
            ITenantProvider tenantProvider,
            IValidator<CadastrarParceiroCommand> validator,
            ILogger<CadastrarParceiroCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioParceiro = repositorioParceiro;
            _tenantProvider = tenantProvider;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<CadastrarParceiroResult>> Handle(
            CadastrarParceiroCommand command, CancellationToken cancellationToken)
        {
            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(resultadoValidacao.Errors.Select(e => e.ErrorMessage)));

            if (await _repositorioParceiro.ExisteParceiroComCnpjAsync(command.Cnpj))
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um parceiro com este CNPJ."));

            try
            {
                var parceiro = new Parceiro(command.Nome, command.Cnpj, command.Categoria)
                {
                    EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
                };

                await _repositorioParceiro.CadastrarAsync(parceiro);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new CadastrarParceiroResult(parceiro.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante o cadastro de parceiro: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
