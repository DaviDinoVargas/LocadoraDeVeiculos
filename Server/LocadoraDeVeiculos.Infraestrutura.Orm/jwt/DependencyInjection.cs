using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.jwt;

public static class DependencyInjection
{
    public static IServiceCollection AddCamadaInfraestruturaJwt(this IServiceCollection services)
    {
        services.AddScoped<AccessTokenProvider>();
        services.AddScoped<RefreshTokenProvider>();

        return services;
    }
}
