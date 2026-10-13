using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
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
            ["CORS_ALLOWED_ORIGINS"] = "http://localhost:4200"
        };

        foreach (var (chave, valor) in configuracoes ?? [])
            _configuracoes[chave] = valor;

        // A API lê as chaves de configuração enquanto monta o host (antes de qualquer callback do
        // WebApplicationFactory), então variáveis de ambiente são o caminho que funciona.
        foreach (var (chave, valor) in _configuracoes)
            Environment.SetEnvironmentVariable(chave, valor);
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
