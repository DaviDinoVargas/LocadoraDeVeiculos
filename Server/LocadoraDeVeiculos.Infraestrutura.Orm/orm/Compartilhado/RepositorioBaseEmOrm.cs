using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;

public class RepositorioBaseEmOrm<T>(LocadoraDeVeiculosDbContext contexto) where T : EntidadeBase<T>
{
    protected readonly DbSet<T> registros = contexto.Set<T>();

    public async Task CadastrarAsync(T novoRegistro)
    {
        await registros.AddAsync(novoRegistro);
    }

    public async Task CadastrarEntidades(IList<T> entidades)
    {
        await registros.AddRangeAsync(entidades);
    }

    public async Task<bool> EditarAsync(Guid idRegistro, T registroEditado)
    {
        var registroSelecionado = await SelecionarPorIdAsync(idRegistro);

        if (registroSelecionado is null)
            return false;

        registroSelecionado.AtualizarRegistro(registroEditado);

        return true;
    }

    public async Task<bool> ExcluirAsync(Guid idRegistro)
    {
        var registroSelecionado = await SelecionarPorIdAsync(idRegistro);

        if (registroSelecionado is null)
            return false;

        registroSelecionado.Excluir();

        return true;
    }

    public virtual async Task<T?> SelecionarPorIdAsync(Guid idRegistro)
    {
        return await registros
            .FirstOrDefaultAsync(x => x.Id.Equals(idRegistro));
    }

    public virtual async Task<List<T>> SelecionarTodosAsync()
    {
        return await registros
            .ToListAsync();
    }
}