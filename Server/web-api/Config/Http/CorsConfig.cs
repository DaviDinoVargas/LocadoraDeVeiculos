using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace LocadoraDeVeiculos.WebApi.Config.Http;

public class CorsConfig(IConfiguration configuration) : IConfigureOptions<CorsOptions>
{
    public void Configure(CorsOptions options)
    {
        var origensPermitidasString = configuration["CORS_ALLOWED_ORIGINS"];

        if (string.IsNullOrWhiteSpace(origensPermitidasString))
            throw new Exception("A variável de ambiente \"CORS_ALLOWED_ORIGINS\" não foi fornecida.");

        var origensPermitidas = origensPermitidasString
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        options.AddDefaultPolicy(policy =>
        {
            policy
                .WithOrigins(origensPermitidas)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    }
}
