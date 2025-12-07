using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;


namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado
{
    public class LocadoraDeVeiculosDbContextFactory
        : IDesignTimeDbContextFactory<LocadoraDeVeiculosDbContext>
    {
        public LocadoraDeVeiculosDbContext CreateDbContext(string[] args)
        {
            // carrega a configuração como no runtime
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<LocadoraDeVeiculosDbContextFactory>() 
                .Build();

            var connectionString = config.GetConnectionString("SQL_CONNECTION_STRING");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new Exception("Connection string 'SQL_CONNECTION_STRING' não encontrada nos User Secrets.");

            var optionsBuilder = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
                .UseSqlServer(connectionString, opt => opt.EnableRetryOnFailure(3));

            return new LocadoraDeVeiculosDbContext(optionsBuilder.Options);
        }
    }
}
