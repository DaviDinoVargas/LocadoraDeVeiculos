using FluentResults;
using FluentValidation;
using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloGrupoAutomovel.Commands;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using MediatR;
using Microsoft.Extensions.Logging;

public class CadastrarGrupoAutomovelCommandHandler
    : IRequestHandler<CadastrarGrupoAutomovelCommand, Result<CadastrarGrupoAutomovelResult>>
{
    private readonly LocadoraDeVeiculosDbContext _appDbContext;
    private readonly IRepositorioGrupoAutomovel _repositorioGrupoAutomovel;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<CadastrarGrupoAutomovelCommand> _validator;
    private readonly ILogger<CadastrarGrupoAutomovelCommandHandler> _logger;

    public CadastrarGrupoAutomovelCommandHandler(
        LocadoraDeVeiculosDbContext appDbContext,
        IRepositorioGrupoAutomovel repositorioGrupoAutomovel,
        ITenantProvider tenantProvider,
        IValidator<CadastrarGrupoAutomovelCommand> validator,
        ILogger<CadastrarGrupoAutomovelCommandHandler> logger
    )
    {
        _appDbContext = appDbContext;
        _repositorioGrupoAutomovel = repositorioGrupoAutomovel;
        _tenantProvider = tenantProvider;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<CadastrarGrupoAutomovelResult>> Handle(
        CadastrarGrupoAutomovelCommand command, CancellationToken cancellationToken)
    {
        // Validação do comando
        var resultadoValidacao = await _validator.ValidateAsync(command, cancellationToken);

        if (!resultadoValidacao.IsValid)
        {
            var erros = resultadoValidacao.Errors.Select(e => e.ErrorMessage);
            return Result.Fail(ResultadosErro.RequisicaoInvalidaErro(erros));
        }

        try
        {
            var grupoAutomovel = new GrupoAutomovel(
                command.Nome,
                command.Descricao
            )
            {
                EmpresaId = _tenantProvider.EmpresaId.GetValueOrDefault()
            };

            await _repositorioGrupoAutomovel.CadastrarAsync(grupoAutomovel);
            await _appDbContext.SaveChangesAsync(cancellationToken);

            return Result.Ok(new CadastrarGrupoAutomovelResult(grupoAutomovel.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ocorreu um erro durante o cadastro de {@Command}.", command);
            return Result.Fail(ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
