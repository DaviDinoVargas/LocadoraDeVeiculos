using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao
{
    public interface IRepositorioConfiguracao : IRepositorio<Configuracao>
    {
        Task<Configuracao?> SelecionarPorEmpresaIdAsync(Guid empresaId);
    }
}