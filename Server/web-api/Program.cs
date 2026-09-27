using LocadoraDeVeiculos.WebApi.Config;
using LocadoraDeVeiculos.WebApi.Config.Http;
using LocadoraDeVeiculos.WebApi.Config.Identify;
using LocadoraDeVeiculos.WebApi.Config.Orm;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LocadoraDeVeiculos.WebApi.Config.Swagger;
using LocadoraDeVeiculos.Core.Aplicacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt;
using Microsoft.AspNetCore.RateLimiting;

namespace LocadoraDeVeiculos.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services
                .AddCamadaInfraestruturaOrm(builder.Configuration)
                .AddCamadaInfraestruturaJwt();

            builder.Services.AddCamadaAplicacao(builder.Configuration);

            builder.Services.AddSwaggerConfig();
            builder.Services.AddIdentityProviderConfig(builder.Configuration);

            builder.Services.ConfigureOptions<CorsConfig>().AddCors();

            builder.Services
                .AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            // Rate limiting: protege endpoints sensíveis (login/registro/rotação de token) contra
            // força bruta e spam de contas. Aplicado via [EnableRateLimiting("auth")] no AuthController.
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            });

            var app = builder.Build();

            // Precisa vir cedo no pipeline para capturar exceções não tratadas de qualquer
            // middleware/controller depois dele, e nunca devolver stack trace ao cliente em produção.
            app.UseGlobalExceptionHandler();

            if (app.Environment.IsDevelopment())
            {
                app.AplicarMigracoesOrm();

                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseCors();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}