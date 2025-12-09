using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAluguel
{
    public interface IRepositorioAluguel : IRepositorio<Aluguel>
    {
        Task<bool> ExisteAluguelAtivoParaAutomovelAsync(Guid automovelId, DateTimeOffset dataSaida, DateTimeOffset dataRetornoPrevisto, Guid? aluguelIdExcluir = null);
        Task<bool> VerificarDocumentosCondutorEmDiaAsync(Guid condutorId);
        Task<List<Aluguel>> SelecionarAlugueisAtivosPorAutomovelAsync(Guid automovelId);
        Task<List<Aluguel>> SelecionarAlugueisPorStatusAsync(StatusAluguel status);
        Task<List<Aluguel>> SelecionarAlugueisEmAbertoAsync();
        Task<Aluguel?> SelecionarAluguelCompletoPorIdAsync(Guid id);
    }
}