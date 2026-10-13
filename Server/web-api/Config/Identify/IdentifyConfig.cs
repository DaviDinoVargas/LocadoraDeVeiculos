using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using System.Security.Claims;

namespace LocadoraDeVeiculos.WebApi.Config.Identify;

public static class IdentityConfig
{
    public static void AddIdentityProviderConfig(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantProvider, IdentityTenantProvider>();

        services.AddIdentity<Usuario, Cargo>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;

            // Proteção contra força bruta: bloqueia a conta após várias tentativas erradas.
            // O bloqueio só é aplicado de fato quando o login passa pelo SignInManager
            // (CheckPasswordSignInAsync com lockoutOnFailure: true), não pelo UserManager sozinho.
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<LocadoraDeVeiculosDbContext>()
        .AddDefaultTokenProviders();

        // PBKDF2 do Identity: o padrão do .NET 8 são 100.000 iterações; o OWASP recomenda 210.000
        // para HMAC-SHA512. Hashes antigos continuam válidos (a contagem fica gravada no hash) e
        // são regravados com o novo custo no próximo login bem-sucedido.
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);

        services.AddJwtAuthentication(configuration);
    }

    private static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var chaveAssinaturaJwt = ChaveJwt.Validar(configuration["JWT_GENERATION_KEY"]);

        var chaveEmBytes = Encoding.ASCII.GetBytes(chaveAssinaturaJwt);

        var audienciaValida = configuration["JWT_AUDIENCE_DOMAIN"];

        if (audienciaValida is null)
            throw new ArgumentException("Não foi possível obter o domínio da audiência dos tokens.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true;
            // O token não é lido depois da validação (as claims bastam); guardá-lo no
            // AuthenticationProperties só o deixaria circulando em memória sem necessidade.
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = "LocadoraDeVeiculos",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(chaveEmBytes),
                ValidAudience = audienciaValida,
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                // O padrão (5 min) é um terço da vida de um token de 15 min: um token vencido
                // continuaria valendo. 30 s cobrem a diferença entre relógios.
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<Usuario>>();

                    var userId = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                    if (!Guid.TryParse(userId, out var uid))
                    {
                        context.HttpContext.Items["AuthErrorCode"] = "InvalidUserId";
                        context.Fail("ID de Usuário inválido.");
                        return;
                    }

                    var user = await userManager.FindByIdAsync(uid.ToString());

                    if (user is null)
                    {
                        context.HttpContext.Items["AuthErrorCode"] = "UserNotFound";
                        context.Fail("Usuário não encontrado.");
                        return;
                    }

                    var verClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                    if (!Guid.TryParse(verClaim, out var tokenVer) || !tokenVer.Equals(user.AccessTokenVersionId))
                    {
                        context.HttpContext.Items["AuthErrorCode"] = "TokenRevoked";
                        context.Fail("O token de acesso do usuário foi revogado.");
                        return;
                    }
                },

                OnChallenge = ctx =>
                {
                    ctx.HandleResponse();

                    if (ctx.Response.HasStarted)
                        return Task.CompletedTask;

                    var http = ctx.HttpContext;
                    var errorCode = http.Items["AuthErrorCode"] as string ?? "Unauthorized";

                    http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    http.Response.ContentType = "application/json";

                    var payload = JsonSerializer.Serialize(new
                    {
                        status = 401,
                        error = errorCode,
                        message = ctx.AuthenticateFailure?.Message ?? "Não foi possível autenticar o usuário."
                    });

                    return http.Response.WriteAsync(payload);
                },

                OnForbidden = ctx =>
                {
                    if (ctx.Response.HasStarted)
                        return Task.CompletedTask;

                    var http = ctx.HttpContext;

                    http.Response.StatusCode = StatusCodes.Status403Forbidden;
                    http.Response.ContentType = "application/json";

                    var payload = JsonSerializer.Serialize(new
                    {
                        status = 403,
                        error = "Forbidden",
                        message = "O usuário autenticado não tem permissão para acessar este recurso."
                    });

                    return http.Response.WriteAsync(payload);
                }
            };
        });
    }
}