using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAuditoria;

public class RepositorioAuditoriaEmOrm(LocadoraDeVeiculosDbContext dbContext) : IRepositorioAuditoria
{
    public async Task<IReadOnlyList<RegistroDeAuditoria>> ListarRecentesAsync(int limite, CancellationToken cancellationToken)
    {
        var registros = await dbContext.Set<RegistroDeAuditoria>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Ordena em memória só depois do filtro por empresa: o volume por tenant é pequeno e isso evita
        // depender do provedor para ordenar por DateTime.
        return registros.OrderByDescending(r => r.EmUtc).Take(limite).ToList();
    }
}
