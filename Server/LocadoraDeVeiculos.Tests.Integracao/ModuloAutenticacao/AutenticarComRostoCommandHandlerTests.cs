using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// Cobre o handler de login facial de ponta a ponta (Identity + emissão real de
// JWT/refresh token), trocando só a parte que faria uma chamada HTTP de
// verdade pro serviço Python de ML por um mock -- o objetivo aqui é garantir
// que nenhum token seja emitido a menos que o serviço de ML confirme o match.
[TestClass]
[TestCategory("Integração - Autenticação Facial")]
public sealed class AutenticarComRostoCommandHandlerTests
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

    private async Task<Usuario> CriarUsuarioEmpresaAsync(string email)
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

        var resultado = await _userManager.CreateAsync(usuario, "Senha@123");
        if (!resultado.Succeeded)
            throw new Exception("Falha ao criar usuário de teste.");

        await _userManager.AddToRoleAsync(usuario, "Empresa");

        return usuario;
    }

    private AutenticarComRostoCommandHandler CriarHandler(IFacialAuthClient facialAuthClient) =>
        new(_userManager, facialAuthClient, _tokenProvider, _refreshTokenProvider, NullLogger<AutenticarComRostoCommandHandler>.Instance);

    [TestMethod]
    public async Task Handle_QuandoRostoBate_EmiteAccessTokenERefreshToken()
    {
        var usuario = await CriarUsuarioEmpresaAsync("bateu@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.VerificarAsync("usuario:bateu@teste.com", "foto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: true, Bateu: true, Confianca: 0.97));

        var handler = CriarHandler(mockFacial.Object);

        var resultado = await handler.Handle(new AutenticarComRostoCommand("bateu@teste.com", "foto"), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(resultado.Value.Item1.Chave));
        Assert.IsFalse(string.IsNullOrWhiteSpace(resultado.Value.Item3));
    }

    [TestMethod]
    public async Task Handle_QuandoRostoNaoBate_NaoEmiteTokenNenhum()
    {
        var usuario = await CriarUsuarioEmpresaAsync("naobateu@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0.2));

        var handler = CriarHandler(mockFacial.Object);

        var resultado = await handler.Handle(new AutenticarComRostoCommand("naobateu@teste.com", "foto-de-outra-pessoa"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    [TestMethod]
    public async Task Handle_QuandoNenhumRostoCadastrado_FalhaComMensagemGenerica()
    {
        var usuario = await CriarUsuarioEmpresaAsync("semcadastro@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: false, Bateu: false, Confianca: 0));

        var handler = CriarHandler(mockFacial.Object);

        var resultado = await handler.Handle(new AutenticarComRostoCommand("semcadastro@teste.com", "foto"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    [TestMethod]
    public async Task Handle_ComEmailNaoCadastrado_FalhaSemChamarServicoDeMl()
    {
        // Nem consulta o serviço de ML: evita revelar se o e-mail existe, e evita
        // gastar uma chamada HTTP externa por nada. MockBehavior.Strict faz o teste
        // falhar se o handler chamar qualquer método do mock sem essa checagem passar antes.
        var mockFacial = new Mock<IFacialAuthClient>(MockBehavior.Strict);
        var handler = CriarHandler(mockFacial.Object);

        var resultado = await handler.Handle(new AutenticarComRostoCommand("naoexiste@teste.com", "foto"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }
}
