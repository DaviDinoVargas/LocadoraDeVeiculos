using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloParceiro
{
    public interface IRepositorioParceiro : IRepositorio<Parceiro>
    {
        Task<bool> ExisteParceiroComCnpjAsync(string cnpj, Guid? idExcluir = null);
        Task<bool> ExisteCupomVinculadoAsync(Guid parceiroId);
    }
}
