using LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloGrupoAutomovel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm;

public static class DependencyInjection
{
    public static IServiceCollection AddCamadaInfraestruturaOrm(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddEntityFrameworkConfig(configuration);

        services.AddScoped<RepositorioFuncionarioEmOrm>();
        services.AddScoped<RepositorioGrupoAutomovelEmOrm>();
        services.AddScoped<RepositorioPlanoCobrancaEmOrm>();

        return services;
    }

    private static void AddEntityFrameworkConfig(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration["SQL_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new Exception("A variável SQL_CONNECTION_STRING não foi fornecida.");

        services.AddDbContext<LocadoraDeVeiculosDbContext>(options =>
            options.UseSqlServer(connectionString, (opt) => opt.EnableRetryOnFailure(3)));
    }
}
