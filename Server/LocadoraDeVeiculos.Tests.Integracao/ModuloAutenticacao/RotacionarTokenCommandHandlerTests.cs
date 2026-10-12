using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// Refresh token inválido (desconhecido, expirado ou já usado) é uma SESSÃO inválida: o resultado
// tem de ser "NaoAutorizado" (a API responde 401 e o front manda para o login), nunca uma exceção
// interna (que virava 500 "Ocorreu um erro interno do servidor").
[TestClass]
[TestCategory("Integração - Rotação de token")]
public sealed class RotacionarTokenCommandHandlerTests
{
    private ServiceProvider _serviceProvider = null!;
    private LocadoraDeVeiculosDbContext _dbContext = null!;
    private UserManager<Usuario> _userManager = null!;
    private AccessTokenProvider _tokenProvider = null!;
    private RefreshTokenProvider _refreshTokenProvider = null!;

    [TestInitialize]
    public async Task Setup()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_GENERATION_KEY"] = "chave-de-teste-bem-grande-1234567890123456",
                ["JWT_AUDIENCE_DOMAIN"] = "localhost"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuracao);
        services.AddDbContext<LocadoraDeVeiculosDbContext>(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentity<Usuario, Cargo>()
            .AddEntityFrameworkStores<LocadoraDeVeiculosDbContext>()
            .AddDefaultTokenProviders();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<LocadoraDeVeiculosDbContext>();
        _userManager = _serviceProvider.GetRequiredService<UserManager<Usuario>>();
        var roleManager = _serviceProvider.GetRequiredService<RoleManager<Cargo>>();

        if (!await roleManager.RoleExistsAsync("Empresa"))
            await roleManager.CreateAsync(new Cargo { Id = Guid.NewGuid(), Name = "Empresa", NormalizedName = "EMPRESA" });

        _tokenProvider = new AccessTokenProvider(_dbContext, _userManager, configuracao);
        _refreshTokenProvider = new RefreshTokenProvider(_dbContext, _userManager, new HttpContextAccessor());
    }

    [TestCleanup]
    public void Cleanup() => _serviceProvider.Dispose();

    private RotacionarTokenCommandHandler CriarHandler() =>
        new(_dbContext, _tokenProvider, _refreshTokenProvider, NullLogger<RotacionarTokenCommandHandler>.Instance);

    private async Task<string> CriarUsuarioComRefreshTokenAsync(string email)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = "Usuário de Teste",
            NormalizedUserName = email.ToUpperInvariant(),
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var criado = await _userManager.CreateAsync(usuario, "Senha@123");
        Assert.IsTrue(criado.Succeeded);
        await _userManager.AddToRoleAsync(usuario, "Empresa");

        var (_, bruto) = await _refreshTokenProvider.GerarRefreshTokenAsync(usuario);
        return bruto;
    }

    private static string TipoDoErro(FluentResults.ResultBase resultado) =>
        (string)resultado.Errors.First().Metadata["TipoErro"];

    [TestMethod]
    public async Task TokenDesconhecido_RetornaNaoAutorizado_EmVezDeErroInterno()
    {
        var resultado = await CriarHandler().Handle(new RotacionarTokenCommand("token-que-nunca-existiu"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.AreEqual("NaoAutorizado", TipoDoErro(resultado));
    }

    [TestMethod]
    public async Task TokenValido_EmiteNovosTokens()
    {
        var bruto = await CriarUsuarioComRefreshTokenAsync("valido@teste.com");

        var resultado = await CriarHandler().Handle(new RotacionarTokenCommand(bruto), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, resultado.IsFailed ? string.Join("; ", resultado.Errors) : "");
        Assert.IsFalse(string.IsNullOrWhiteSpace(resultado.Value.Item1.Chave));
        Assert.AreNotEqual(bruto, resultado.Value.Item3);
    }

    [TestMethod]
    public async Task TokenJaUsado_NaoRotacionaDeNovo_ENaoEhErroInterno()
    {
        var bruto = await CriarUsuarioComRefreshTokenAsync("reuso@teste.com");
        var handler = CriarHandler();

        var primeira = await handler.Handle(new RotacionarTokenCommand(bruto), CancellationToken.None);
        var segunda = await handler.Handle(new RotacionarTokenCommand(bruto), CancellationToken.None);

        Assert.IsTrue(primeira.IsSuccess);
        Assert.IsTrue(segunda.IsFailed);
        Assert.AreEqual("NaoAutorizado", TipoDoErro(segunda));
    }

    [TestMethod]
    public async Task TokenExpirado_RetornaNaoAutorizado()
    {
        var bruto = await CriarUsuarioComRefreshTokenAsync("expirado@teste.com");
        var token = await _dbContext.RefreshTokens.SingleAsync();
        token.ExpiraEmUtc = DateTime.UtcNow.AddMinutes(-1);
        await _dbContext.SaveChangesAsync();

        var resultado = await CriarHandler().Handle(new RotacionarTokenCommand(bruto), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.AreEqual("NaoAutorizado", TipoDoErro(resultado));
    }

    [TestMethod]
    public async Task ErroInesperado_ContinuaSendoErroInterno_ENaoVazaOTokenNoResultado()
    {
        var bruto = await CriarUsuarioComRefreshTokenAsync("falha@teste.com");
        _dbContext.Dispose();   // força uma exceção que NÃO é de token

        var resultado = await CriarHandler().Handle(new RotacionarTokenCommand(bruto), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.AreEqual("ExcecaoInterna", TipoDoErro(resultado));
        Assert.IsFalse(resultado.Errors.Any(e => e.Message.Contains(bruto)));
    }
}
