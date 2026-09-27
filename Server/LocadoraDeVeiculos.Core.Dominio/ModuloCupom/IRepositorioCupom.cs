using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloCupom
{
    public interface IRepositorioCupom : IRepositorio<Cupom>
    {
        Task<bool> ExisteCupomComCodigoAsync(string codigo, Guid? idExcluir = null);
        Task<Cupom?> SelecionarPorCodigoAsync(string codigo);
        Task<bool> ExisteAluguelVinculadoAsync(Guid cupomId);
    }
}
