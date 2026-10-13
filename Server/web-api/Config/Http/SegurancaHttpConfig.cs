using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace LocadoraDeVeiculos.WebApi.Config.Http;

/// <summary>
/// Proteções de borda da API: IP real atrás de proxy (S4), limite de tamanho de corpo (S9),
/// limite de requisições global e para autenticação (S10) e cabeçalhos de segurança (S8).
/// </summary>
public static class SegurancaHttpConfig
{
    public const string PoliticaAutenticacao = "auth";
    public const string PoliticaRegistro = "registro";

    // Corpo máximo aceito pela API. As maiores cargas reais são imagens de webcam (640x480,
    // ~100 KB em base64); o padrão do Kestrel (~30 MB) só serve para quem quer estourar memória.
    public const long LimiteCorpoPadraoEmBytes = 2 * 1024 * 1024;

    public const int LimiteGlobalPorMinutoPadrao = 300;
    public const int LimiteAutenticacaoPorMinuto = 10;
    public const int LimiteRegistroPorHoraPadrao = 5;

    public static IServiceCollection AddSegurancaHttp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(opcoes => ConfigurarProxiesConfiaveis(opcoes, configuration));

        var limiteCorpo = LerInteiroPositivo(configuration, "MAX_REQUEST_BODY_BYTES", LimiteCorpoPadraoEmBytes);
        services.Configure<KestrelServerOptions>(opcoes => opcoes.Limits.MaxRequestBodySize = limiteCorpo);

        var limiteGlobal = (int)LerInteiroPositivo(configuration, "RATE_LIMIT_PER_MINUTE", LimiteGlobalPorMinutoPadrao);
        var limiteRegistro = (int)LerInteiroPositivo(configuration, "REGISTRATION_LIMIT_PER_HOUR", LimiteRegistroPorHoraPadrao);
        services.AddRateLimiter(opcoes => ConfigurarLimitesDeRequisicao(opcoes, limiteGlobal, limiteRegistro));

        return services;
    }

    /// <summary>
    /// Só aceita X-Forwarded-For/Proto de proxies listados em TRUSTED_PROXIES (IPs ou CIDR separados por ';').
    /// Sem a lista, o padrão do ASP.NET Core vale (apenas loopback), então um cliente qualquer não
    /// consegue forjar o próprio IP para escapar do rate limit.
    /// </summary>
    public static void ConfigurarProxiesConfiaveis(ForwardedHeadersOptions opcoes, IConfiguration configuration)
    {
        opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        opcoes.ForwardLimit = 1;

        var lista = configuration["TRUSTED_PROXIES"];

        if (string.IsNullOrWhiteSpace(lista))
            return;

        foreach (var item in lista.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var partes = item.Split('/', 2);

            if (!IPAddress.TryParse(partes[0], out var endereco))
                throw new ArgumentException($"TRUSTED_PROXIES contém um endereço inválido: \"{item}\".");

            if (partes.Length == 1)
            {
                opcoes.KnownProxies.Add(endereco);
                continue;
            }

            if (!int.TryParse(partes[1], out var prefixo))
                throw new ArgumentException($"TRUSTED_PROXIES contém um prefixo inválido: \"{item}\".");

            opcoes.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(endereco, prefixo));
        }
    }

    public static void ConfigurarLimitesDeRequisicao(
        RateLimiterOptions opcoes, int limiteGlobalPorMinuto, int limiteRegistroPorHora = LimiteRegistroPorHoraPadrao)
    {
        opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Limite geral: por usuário autenticado (sub) ou, sem login, por IP.
        opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ChaveDoSolicitante(contexto),
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limiteGlobalPorMinuto,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        // Login, cadastro e rotação de token: força bruta e spam de contas. Sempre por IP, porque
        // nesses pontos ainda não há usuário autenticado.
        opcoes.AddPolicy(PoliticaAutenticacao, contexto => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ChaveDoIp(contexto.Connection.RemoteIpAddress),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = LimiteAutenticacaoPorMinuto,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        // Criar conta cria uma empresa (tenant) inteira: spam de contas enche o banco e consome ML/câmeras.
        opcoes.AddPolicy(PoliticaRegistro, contexto => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ChaveDoIp(contexto.Connection.RemoteIpAddress),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limiteRegistroPorHora,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
    }

    private static string ChaveDoSolicitante(HttpContext contexto)
    {
        var usuario = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? contexto.User.FindFirstValue("sub");

        return usuario is not null
            ? $"usuario:{usuario}"
            : $"ip:{ChaveDoIp(contexto.Connection.RemoteIpAddress)}";
    }

    /// <summary>
    /// Chave do balde de rate limit para um IP. Em IPv6 uma pessoa costuma receber um /64 inteiro (2^64 endereços): limitar por
    /// endereço permitiria trocar de IP a cada pedido e nunca atingir o limite. Por isso IPv6 vale pelo prefixo /64.
    /// </summary>
    public static string ChaveDoIp(IPAddress? endereco)
    {
        if (endereco is null)
            return "desconhecido";

        if (endereco.IsIPv4MappedToIPv6)
            endereco = endereco.MapToIPv4();

        if (endereco.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            return endereco.ToString();

        var bytes = endereco.GetAddressBytes();
        Array.Clear(bytes, 8, 8);

        return new IPAddress(bytes) + "/64";
    }

    /// <summary>
    /// Cabeçalhos de uma API que só devolve JSON: nada de ser embutida em frame, interpretada como outro
    /// tipo, cacheada ou usada para vazar o endereço de origem. A CSP bloqueia tudo (não há HTML aqui);
    /// o Swagger de desenvolvimento fica de fora porque precisa de scripts e estilos.
    /// </summary>
    public static IApplicationBuilder UseCabecalhosDeSeguranca(this IApplicationBuilder app, bool ambienteDeDesenvolvimento)
    {
        return app.Use(async (contexto, proximo) =>
        {
            contexto.Response.OnStarting(() =>
            {
                var cabecalhos = contexto.Response.Headers;

                cabecalhos["X-Content-Type-Options"] = "nosniff";
                cabecalhos["X-Frame-Options"] = "DENY";
                cabecalhos["Referrer-Policy"] = "no-referrer";
                cabecalhos["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                var ehSwagger = contexto.Request.Path.StartsWithSegments("/swagger");

                if (!(ambienteDeDesenvolvimento && ehSwagger))
                {
                    cabecalhos["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                    cabecalhos["Cache-Control"] = "no-store";
                }

                return Task.CompletedTask;
            });

            await proximo();
        });
    }

    private static long LerInteiroPositivo(IConfiguration configuration, string chave, long padrao)
    {
        var texto = configuration[chave];

        if (string.IsNullOrWhiteSpace(texto))
            return padrao;

        if (!long.TryParse(texto, out var valor) || valor <= 0)
            throw new ArgumentException($"A variável \"{chave}\" precisa ser um número inteiro positivo.");

        return valor;
    }
}
