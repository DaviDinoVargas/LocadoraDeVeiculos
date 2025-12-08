using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel
{
    public interface IRepositorioAutomovel : IRepositorio<Automovel>
    {
        Task<bool> ExisteAutomovelComPlacaAsync(string placa, Guid? idExcluir = null);
        Task<bool> ExisteAluguelEmAbertoAsync(Guid automovelId);
        Task<List<Automovel>> SelecionarPorGrupoAsync(Guid grupoId);
    }
}