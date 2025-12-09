using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloCliente
{
    public interface IRepositorioCliente : IRepositorio<Cliente>
    {
        Task<bool> ExisteClienteComCpfAsync(string cpf, Guid? idExcluir = null);
        Task<bool> ExisteClienteComCnpjAsync(string cnpj, Guid? idExcluir = null);
        Task<bool> ExisteAluguelEmAbertoAsync(Guid clienteId);
        Task<List<ClientePessoaFisica>> SelecionarPessoasFisicasAsync();
        Task<List<ClientePessoaJuridica>> SelecionarPessoasJuridicasAsync();
        Task<ClientePessoaFisica?> SelecionarPessoaFisicaPorIdAsync(Guid id);
        Task<ClientePessoaJuridica?> SelecionarPessoaJuridicaPorIdAsync(Guid id);
    }
}