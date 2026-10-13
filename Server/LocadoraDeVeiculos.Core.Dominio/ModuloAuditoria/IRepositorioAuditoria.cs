using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;

public interface IRepositorioAuditoria
{
    /// <summary>Os registros mais recentes da empresa do usuário autenticado (o isolamento é feito no contexto).</summary>
    Task<IReadOnlyList<RegistroDeAuditoria>> ListarRecentesAsync(int limite, CancellationToken cancellationToken);
}
