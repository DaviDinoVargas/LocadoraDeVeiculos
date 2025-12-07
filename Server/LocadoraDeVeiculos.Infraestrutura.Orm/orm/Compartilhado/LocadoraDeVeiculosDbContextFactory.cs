using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;

public static class LocadoraDeVeiculosDbContextFactory
{
    public static LocadoraDeVeiculosDbContext CriarDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseSqlServer(connectionString, opt => opt.EnableRetryOnFailure(3))
            .Options;

        // TenantProvider nulo por padrão
        return new LocadoraDeVeiculosDbContext(options);
    }
}
