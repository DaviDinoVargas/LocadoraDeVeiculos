using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocadoraDeVeiculos.Tests.WebApi.Compartilhado;

/// <summary>
/// Sobe a Web API de verdade (pipeline, autenticação, autorização, rate limit) trocando só o SQL Server
/// por um banco em memória. Os segredos que a API exige na subida recebem valores de teste.
/// </summary>
public class ApiFactory : WebApplicationFactory<LocadoraDeVeiculos.WebApi.Program>
{
    public const string ChaveJwt = "chave-de-teste-com-mais-de-trinta-e-dois-caracteres";

    private readonly Dictionary<string, string> _configuracoes;

    public ApiFactory(Dictionary<string, string>? configuracoes = null)
    {
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
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        foreach (var chave in _configuracoes.Keys)
            Environment.SetEnvironmentVariable(chave, null);
    }
}
