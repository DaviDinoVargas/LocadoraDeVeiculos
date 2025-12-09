using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloCondutor
{
    public interface IRepositorioCondutor : IRepositorio<Condutor>
    {
        Task<bool> ExisteCondutorComCpfAsync(string cpf, Guid? idExcluir = null);
        Task<bool> ExisteCondutorComCnhAsync(string cnh, Guid? idExcluir = null);
        Task<bool> ExisteAluguelEmAbertoAsync(Guid condutorId);
        Task<List<Condutor>> SelecionarPorClienteAsync(Guid clienteId);
    }
}