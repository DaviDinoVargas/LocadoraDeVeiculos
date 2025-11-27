using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Microsoft.AspNetCore.Identity;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.Identify;
using LocadoraDeVeiculos.WebApi.Config;

namespace LocadoraDeVeiculos.WebApi
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Logging [env NEWRELIC_LICENSE_KEY]
            builder.Services.ConfigureSerilog(builder.Logging, builder.Configuration);

            // Database Provider [env SQL_CONNECTION_STRING]
            builder.Services.ConfigureDbContext(builder.Configuration, builder.Environment);

            // Validation
            builder.Services.ConfigureFluentValidation();

            // Services
            builder.Services.ConfigureRepositories();
            builder.Services.ConfigureMediatR();

            // Auth [env JWT_GENERATION_KEY, JWT_AUDIENCE_DOMAIN]
            builder.Services.ConfigureIdentityProviders();
            builder.Services.ConfigureJwtAuthentication(builder.Configuration);

            // Controllers
            builder.Services.ConfigureControllersWithFilters();

            // API Documentation 
            builder.Services.ConfigureOpenApiAuthHeaders();

            // CORS [env CORS_ALLOWED_ORIGINS]
            builder.Services.ConfigureCorsPolicy(builder.Environment, builder.Configuration);

            var app = builder.Build();

            app.UseGlobalExceptionHandler();

            app.AutoMigrateDatabase();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseHttpsRedirection();

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            try
            {
                // === Role seeding: garante que os cargos existam no banco ===
                using (var scope = app.Services.CreateScope())
                {
                    var serviceProvider = scope.ServiceProvider;

                    // Obter o RoleManager<Cargo> (lança se não registrado)
                    var roleManager = serviceProvider.GetRequiredService<RoleManager<Cargo>>();

                    // Chama o seeder (assíncrono)
                    await RoleSeeder.CriarCargosAsync(roleManager);
                }

                // Executa a aplicação (assíncrono)
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal("Ocorreu um erro fatal durante a execução da aplicação: {@Excecao}", ex);
                throw; // opcional: re-throw para garantir código de saída != 0
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}