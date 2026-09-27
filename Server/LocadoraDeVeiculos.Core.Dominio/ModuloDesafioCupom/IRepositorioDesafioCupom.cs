using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom
{
    public interface IRepositorioDesafioCupom : IRepositorio<DesafioCupom>
    {
        /// <summary>
        /// Conta quantos aluguéis concluídos o cliente tem dentro da janela de dias do desafio,
        /// para checar se ele já cumpriu a meta de quantidade.
        /// </summary>
        Task<int> ContarAlugueisConcluidosDoClienteAsync(Guid clienteId, int periodoDias);
    }
}
