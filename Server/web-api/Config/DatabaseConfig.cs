using LocadoraDeVeiculos.Infraestrutura.Orm.Compartilhado;

namespace LocadoraDeVeiculos.WebApi.Config;

public static class DatabaseConfig
{
    public static bool AutoMigrateDatabase(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<LocadoraDeVeiculosMDbContext>();

        var migracaoConcluida = MigradorBancoDados.AtualizarBancoDados(dbContext);

        return migracaoConcluida;
    }
}