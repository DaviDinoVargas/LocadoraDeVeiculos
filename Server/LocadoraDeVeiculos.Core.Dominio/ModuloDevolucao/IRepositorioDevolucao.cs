using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao
{
    public interface IRepositorioDevolucao : IRepositorio<Devolucao>
    {
        Task<bool> ExisteDevolucaoParaAluguelAsync(Guid aluguelId);
        Task<Devolucao?> SelecionarPorAluguelIdAsync(Guid aluguelId);
    }
}