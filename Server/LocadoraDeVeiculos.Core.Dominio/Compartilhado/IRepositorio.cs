using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.Compartilhado;

public interface IRepositorio<T> where T : EntidadeBase<T>
{
    Task CadastrarAsync(T novoRegistro);
    Task<bool> EditarAsync(Guid id, T registroEditado);
    Task<bool> ExcluirAsync(Guid id);

    Task<List<T>> SelecionarTodosAsync();
    Task<List<T>> SelecionarTodosAsync(int quantity);

    Task<T?> SelecionarPorIdAsync(Guid id);
}