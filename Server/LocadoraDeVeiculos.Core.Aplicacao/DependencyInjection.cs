using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using FluentValidation;

namespace LocadoraDeVeiculos.Core.Aplicacao;

public static class DependencyInjection
{
    public static IServiceCollection AddCamadaAplicacao(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        var licenseKey = configuration["LUCKYPENNY_LICENSE_KEY"];

        if (string.IsNullOrWhiteSpace(licenseKey))
            throw new Exception("A variável LUCKYPENNY_LICENSE_KEY não foi fornecida.");

        services.AddMediatR(config =>
        {
            config.LicenseKey = licenseKey;
            config.RegisterServicesFromAssembly(assembly);
        });

        return services;
    }
}
