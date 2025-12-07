using LocadoraDeVeiculos.WebApi.Config.Http;
using LocadoraDeVeiculos.WebApi.Config.Identify;
using LocadoraDeVeiculos.WebApi.Config.Orm;
using System.Text.Json.Serialization;
using LocadoraDeVeiculos.WebApi.Config.Swagger;
using LocadoraDeVeiculos.Core.Aplicacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt;

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

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.AplicarMigracoesOrm();

                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}