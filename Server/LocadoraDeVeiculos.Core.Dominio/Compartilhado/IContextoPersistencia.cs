using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.Compartilhado
{
    public interface IContextoPersistencia
    {
        Task<int> GravarAsync();
        Task RollbackAsync();
    }
}
