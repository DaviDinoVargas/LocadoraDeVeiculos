using LocadoraDeVeiculos.Infraestrutura.Orm.Compartilhado;
using System;

namespace LocadoraDeVeiculos.WebApi.Config.Orm;

public static class DatabaseOperations
{
    public static void AplicarMigracoesOrm(this IHost app)
    {
        var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.Migrate();
    }
}