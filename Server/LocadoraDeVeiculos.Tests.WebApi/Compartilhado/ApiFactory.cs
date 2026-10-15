using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocadoraDeVeiculos.Tests.WebApi.Compartilhado;

/// <summary>
/// Sobe a Web API de verdade (pipeline, autenticação, autorização, rate limit) trocando só o SQL Server
/// por um banco em memória. Os segredos que a API exige na subida recebem valores de teste.
/// </summary>
internal sealed class IpDeTesteStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (contexto, proximo) =>
        {
            if (contexto.Request.Headers.TryGetValue("X-Test-Ip", out var ip) && System.Net.IPAddress.TryParse(ip.ToString(), out var endereco))
                contexto.Connection.RemoteIpAddress = endereco;

            await proximo();
        });
        next(app);
    };
}

public class ApiFactory : WebApplicationFactory<LocadoraDeVeiculos.WebApi.Program>
{
    public const string ChaveJwt = "chave-de-teste-com-mais-de-trinta-e-dois-caracteres";

    private readonly Dictionary<string, string> _configuracoes;
    private readonly TimeProvider? _relogio;

    /// <param name="relogio">Opcional: troca o relógio da API (TimeProvider) para testar regras que dependem da data, como o painel.</param>
    public ApiFactory(Dictionary<string, string>? configuracoes = null, TimeProvider? relogio = null)
    {
        _relogio = relogio;
        _configuracoes = new Dictionary<string, string>
        {
            ["SQL_CONNECTION_STRING"] = "Server=nao-usado;Database=teste",
            ["JWT_GENERATION_KEY"] = ChaveJwt,
            ["JWT_AUDIENCE_DOMAIN"] = "localhost",
            ["LUCKYPENNY_LICENSE_KEY"] = "chave-de-teste",
            ["CORS_ALLOWED_ORIGINS"] = "http://localhost:4200",
            ["MlApi__InternalToken"] = "segredo-de-teste-com-mais-de-trinta-e-dois-caracteres",
            ["FACE_ENCRYPTION_KEYS"] = "1:AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8="
        };

        foreach (var (chave, valor) in configuracoes ?? [])
            _configuracoes[chave] = valor;

        // A API lê as chaves de configuração enquanto monta o host (antes de qualquer callback do
        // WebApplicationFactory), então variáveis de ambiente são o caminho que funciona.
        foreach (var (chave, valor) in _configuracoes)
            Environment.SetEnvironmentVariable(chave, valor);
    }

    /// <summary>
    /// Cria um usuário com o cargo pedido e devolve um JWT válido para ele, assinado com a mesma chave
    /// que a API usa, para testar autorização sem passar pelo fluxo de login.
    /// </summary>
    public async Task<string> CriarTokenAsync(string cargo, Guid? empresaId = null)
    {
        using var escopo = Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var cargos = escopo.ServiceProvider.GetRequiredService<RoleManager<Cargo>>();

        if (!await cargos.RoleExistsAsync(cargo))
            await cargos.CreateAsync(new Cargo { Id = Guid.NewGuid(), Name = cargo, NormalizedName = cargo.ToUpperInvariant() });

        var usuario = new Usuario
        {
            UserName = $"{Guid.NewGuid():N}@teste.com",
            Email = $"{Guid.NewGuid():N}@teste.com",
            FullName = "Usuário de Teste",
            AccessTokenVersionId = Guid.NewGuid()
        };

        var criado = await usuarios.CreateAsync(usuario, "Senha@12345");
        if (!criado.Succeeded)
            throw new InvalidOperationException(string.Join("; ", criado.Errors.Select(e => e.Description)));

        await usuarios.AddToRoleAsync(usuario, cargo);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, usuario.AccessTokenVersionId.ToString()),
            new(ClaimTypes.Role, cargo),
            new("EmpresaId", (empresaId ?? usuario.Id).ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "LocadoraDeVeiculos",
            audience: "localhost",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(ChaveJwt)), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Cria uma conta com e-mail e senha conhecidos, para testar o login de verdade.</summary>
    public async Task CriarContaAsync(string email, string senha, string cargo = "Empresa")
    {
        using var escopo = Services.CreateScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var cargos = escopo.ServiceProvider.GetRequiredService<RoleManager<Cargo>>();

        if (!await cargos.RoleExistsAsync(cargo))
            await cargos.CreateAsync(new Cargo { Id = Guid.NewGuid(), Name = cargo, NormalizedName = cargo.ToUpperInvariant() });

        var usuario = new Usuario { UserName = email, Email = email, FullName = "Conta de Teste", AccessTokenVersionId = Guid.NewGuid() };
        var criado = await usuarios.CreateAsync(usuario, senha);
        if (!criado.Succeeded)
            throw new InvalidOperationException(string.Join("; ", criado.Errors.Select(e => e.Description)));

        await usuarios.AddToRoleAsync(usuario, cargo);
    }

    /// <summary>Faz o login com o cabeçalho X-Test-Ip, que o servidor de teste (sem IP de verdade) usa como endereço remoto.</summary>
    public async Task<HttpResponseMessage> LoginDoIpAsync(string ip, string email, string senha)
    {
        using var cliente = CreateClient();
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/auth/autenticar")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { email, senha })
        };
        requisicao.Headers.Add("X-Test-Ip", ip);
        return await cliente.SendAsync(requisicao);
    }

    public HttpClient CriarClienteAutenticado(string token)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var nomeDoBanco = Guid.NewGuid().ToString();

        builder.ConfigureServices(servicos =>
        {
            var opcoes = servicos.Where(d => d.ServiceType == typeof(DbContextOptions<LocadoraDeVeiculosDbContext>)).ToList();
            foreach (var descritor in opcoes)
                servicos.Remove(descritor);

            servicos.AddDbContext<LocadoraDeVeiculosDbContext>(o => o.UseInMemoryDatabase(nomeDoBanco));

            if (_relogio is not null)
            {
                foreach (var descritor in servicos.Where(d => d.ServiceType == typeof(TimeProvider)).ToList())
                    servicos.Remove(descritor);
                servicos.AddSingleton(_relogio);
            }

            // O TestServer não tem IP remoto; com X-Test-Ip o teste escolhe de "onde" a requisição vem.
            servicos.AddTransient<IStartupFilter, IpDeTesteStartupFilter>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var chave in _configuracoes.Keys)
            Environment.SetEnvironmentVariable(chave, null);
    }
}
