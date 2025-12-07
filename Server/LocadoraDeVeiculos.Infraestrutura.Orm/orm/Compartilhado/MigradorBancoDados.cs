using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;

public static class MigradorBancoDados
{
    public static bool AtualizarBancoDados(DbContext dbContext)
    {
        var qtdMigracoesPendentes = dbContext.Database.GetPendingMigrations().Count();

        if (qtdMigracoesPendentes == 0) return false;

        dbContext.Database.Migrate();

        return true;
    }
}

