using LocadoraDeVeiculos.WebApi.Config;
using LocadoraDeVeiculos.WebApi.Config.Http;
using LocadoraDeVeiculos.WebApi.Config.Identify;
using LocadoraDeVeiculos.WebApi.Config.Orm;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using LocadoraDeVeiculos.WebApi.Config.Swagger;
using LocadoraDeVeiculos.Core.Aplicacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt;

namespace LocadoraDeVeiculos.WebApi
{
    public class Program
    {
        public static int Main(string[] args)
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
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

                    // Defesa em profundidade contra XSS: "<", ">", "&" e aspas saem escapados (<...) mesmo que um
                    // cliente interprete o JSON como HTML. Acentos continuam legíveis.
                    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(new TextEncoderSettings(UnicodeRanges.All));
                });

            // Borda da API: IP real atrás de proxy, limite de corpo, rate limit global e de autenticação
            // (a política "auth" é aplicada via [EnableRateLimiting] no AuthController).
            builder.Services.AddSegurancaHttp(builder.Configuration);

            var app = builder.Build();

            // Comando de manutenção (S1): importa o antigo faces.json para o banco e sai, sem subir o servidor.
            if (ImportadorDeFaces.FoiSolicitado(args))
            {
                app.AplicarMigracoesOrm();

                return ImportadorDeFaces.ExecutarAsync(app.Services, args, Console.Out).GetAwaiter().GetResult();
            }

            // Primeiro de tudo: troca o IP do proxy pelo do cliente (só se o proxy estiver em TRUSTED_PROXIES).
            app.UseForwardedHeaders();
            app.UseCabecalhosDeSeguranca(app.Environment.IsDevelopment());

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
            app.UseAuthentication();
            // Depois da autenticação para que o limite geral seja por usuário e não só por IP.
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapControllers();

            // AllowedHosts "*" é o padrão de desenvolvimento; num ambiente real deve listar os domínios da API.
            if (!app.Environment.IsDevelopment() && app.Configuration["AllowedHosts"] == "*")
                app.Logger.LogWarning("AllowedHosts está como \"*\": defina os hosts permitidos (AllowedHosts) neste ambiente.");

            app.Run();

            return 0;
        }
    }
}