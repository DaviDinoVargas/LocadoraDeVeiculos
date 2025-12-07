using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;

public class RepositorioFuncionarioEmOrm : RepositorioBaseEmOrm<Funcionario>
{
    private readonly LocadoraDeVeiculosDbContext dbContext;

    public RepositorioFuncionarioEmOrm(LocadoraDeVeiculosDbContext dbContext) : base(dbContext)
    {
        this.dbContext = dbContext;
    }

    public override async Task<Funcionario?> SelecionarPorIdAsync(Guid funcionarioId)
    {
        return await dbContext.Funcionarios
            .Include(u => u.Empresa)
            .Include(u => u.Usuario)
            .FirstOrDefaultAsync(f => f.Id == funcionarioId);
    }

    public async Task<Funcionario?> SelecionarPorUsuarioIdAsync(Guid usuarioId)
    {
        return await dbContext.Funcionarios
            .IgnoreQueryFilters()
            .Include(u => u.Empresa)
            .Include(u => u.Usuario)
            .FirstOrDefaultAsync(f => f.UsuarioId == usuarioId);
    }

    public override async Task<List<Funcionario>> SelecionarTodosAsync()
    {
        return await dbContext.Funcionarios
            .Include(u => u.Empresa)
            .Include(u => u.Usuario)
            .ToListAsync();
    }
}
