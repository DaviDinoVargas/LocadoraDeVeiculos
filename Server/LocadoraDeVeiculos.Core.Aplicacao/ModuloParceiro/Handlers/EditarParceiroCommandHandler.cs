using FluentResults;
using FluentValidation;
using FluentValidation.Results;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloParceiro.Commands;
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
    public class EditarParceiroCommandHandler : IRequestHandler<EditarParceiroCommand, Result<EditarParceiroResult>>
    {
        private readonly LocadoraDeVeiculosDbContext _appDbContext;
        private readonly IRepositorioParceiro _repositorioParceiro;
        private readonly IValidator<EditarParceiroCommand> _validator;
        private readonly ILogger<EditarParceiroCommandHandler> _logger;

        public EditarParceiroCommandHandler(
            LocadoraDeVeiculosDbContext appDbContext,
            IRepositorioParceiro repositorioParceiro,
            IValidator<EditarParceiroCommand> validator,
            ILogger<EditarParceiroCommandHandler> logger)
        {
            _appDbContext = appDbContext;
            _repositorioParceiro = repositorioParceiro;
            _validator = validator;
            _logger = logger;
        }

        public async Task<Result<EditarParceiroResult>> Handle(
            EditarParceiroCommand command, CancellationToken cancellationToken)
        {
            var registroEncontrado = await _repositorioParceiro.SelecionarPorIdAsync(command.Id);

            if (registroEncontrado is null)
                return Result.Fail(ResultadosErro.RegistroNaoEncontradoErro(command.Id));

            ValidationResult resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

            if (!resultadoValidacao.IsValid)
                return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(resultadoValidacao.Errors.Select(e => e.ErrorMessage)));

            if (await _repositorioParceiro.ExisteParceiroComCnpjAsync(command.Cnpj, command.Id))
                return Result.Fail(ResultadosErro.RegistroDuplicadoErro("Já existe um parceiro com este CNPJ."));

            try
            {
                var parceiroEditado = new Parceiro(command.Nome, command.Cnpj, command.Categoria)
                {
                    Ativo = command.Ativo
                };

                await _repositorioParceiro.EditarAsync(command.Id, parceiroEditado);
                await _appDbContext.SaveChangesAsync(cancellationToken);

                return Result.Ok(new EditarParceiroResult(command.Nome, command.Cnpj, command.Categoria, command.Ativo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro durante a edição de parceiro: {@Command}.", command);
                return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
            }
        }
    }
}
