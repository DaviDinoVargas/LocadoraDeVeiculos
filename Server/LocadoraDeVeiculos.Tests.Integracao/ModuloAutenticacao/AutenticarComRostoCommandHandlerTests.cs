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

    // S3: o rosto passa pelo mesmo bloqueio do login por senha.
    private static Mock<IFacialAuthClient> FacialQueResponde(FacialVerifyResult resultado)
    {
        var mock = new Mock<IFacialAuthClient>();
        mock.Setup(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        return mock;
    }

    [TestMethod]
    public async Task Handle_RostosErradosEmSequencia_BloqueiamAContaMesmoQuandoDepoisORostoBate()
    {
        var usuario = await CriarUsuarioEmpresaAsync("bloqueio@teste.com");
        var comando = new AutenticarComRostoCommand("bloqueio@teste.com", "foto");

        var errado = CriarHandler(FacialQueResponde(new FacialVerifyResult(true, false, 0.1)).Object);
        for (var i = 0; i < 5; i++)
            Assert.IsTrue((await errado.Handle(comando, CancellationToken.None)).IsFailed);

        Assert.IsTrue(await _userManager.IsLockedOutAsync(usuario));

        // Com a conta bloqueada nem o rosto certo entra, e o serviço de ML nem é consultado.
        var mockCerto = FacialQueResponde(new FacialVerifyResult(true, true, 0.99));
        var resultado = await CriarHandler(mockCerto.Object).Handle(comando, CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        mockCerto.Verify(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_SemRostoCadastrado_ContaComoFalhaNoBloqueio()
    {
        var usuario = await CriarUsuarioEmpresaAsync("semrosto@teste.com");

        var handler = CriarHandler(FacialQueResponde(new FacialVerifyResult(false, false, 0)).Object);
        await handler.Handle(new AutenticarComRostoCommand("semrosto@teste.com", "foto"), CancellationToken.None);

        Assert.AreEqual(1, await _userManager.GetAccessFailedCountAsync(usuario));
    }

    [TestMethod]
    public async Task Handle_FotoSemRostoDetectavel_NaoContaComoTentativa()
    {
        var usuario = await CriarUsuarioEmpresaAsync("fotoruim@teste.com");

        var handler = CriarHandler(FacialQueResponde(new FacialVerifyResult(true, false, 0, NenhumRostoNaImagem: true)).Object);
        await handler.Handle(new AutenticarComRostoCommand("fotoruim@teste.com", "foto"), CancellationToken.None);

        Assert.AreEqual(0, await _userManager.GetAccessFailedCountAsync(usuario));
    }

    [TestMethod]
    public async Task Handle_RostoQueBate_ZeraOContadorDeFalhas()
    {
        var usuario = await CriarUsuarioEmpresaAsync("zera@teste.com");
        var comando = new AutenticarComRostoCommand("zera@teste.com", "foto");

        await CriarHandler(FacialQueResponde(new FacialVerifyResult(true, false, 0.1)).Object).Handle(comando, CancellationToken.None);
        Assert.AreEqual(1, await _userManager.GetAccessFailedCountAsync(usuario));

        var resultado = await CriarHandler(FacialQueResponde(new FacialVerifyResult(true, true, 0.99)).Object).Handle(comando, CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.AreEqual(0, await _userManager.GetAccessFailedCountAsync(usuario));
    }
}
