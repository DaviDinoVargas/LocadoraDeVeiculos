using FluentResults;
using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAuditoria;

public record ListarAuditoriaQuery(int Limite = 100) : IRequest<Result<ListarAuditoriaResult>>;

public record AuditoriaDto(
    Guid Id,
    DateTime EmUtc,
    Guid? UsuarioId,
    string Acao,
    string Entidade,
    Guid? EntidadeId,
    string? CamposAlterados,
    string? EnderecoIp);

public record ListarAuditoriaResult(IReadOnlyList<AuditoriaDto> Registros);

public class ListarAuditoriaQueryHandler(
    IRepositorioAuditoria repositorio,
    ILogger<ListarAuditoriaQueryHandler> logger
) : IRequestHandler<ListarAuditoriaQuery, Result<ListarAuditoriaResult>>
{
    public const int LimiteMaximo = 500;

    public async Task<Result<ListarAuditoriaResult>> Handle(ListarAuditoriaQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var limite = Math.Clamp(query.Limite, 1, LimiteMaximo);

            var registros = await repositorio.ListarRecentesAsync(limite, cancellationToken);

            var dtos = registros
                .Select(r => new AuditoriaDto(r.Id, r.EmUtc, r.UsuarioId, r.Acao, r.Entidade, r.EntidadeId, r.CamposAlterados, r.EnderecoIp))
                .ToList();

            return Result.Ok(new ListarAuditoriaResult(dtos));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocorreu um erro ao listar a auditoria.");

            return Result.Fail(Compartilhado.ResultadosErro.ExcecaoInternaErro(ex));
        }
    }
}
