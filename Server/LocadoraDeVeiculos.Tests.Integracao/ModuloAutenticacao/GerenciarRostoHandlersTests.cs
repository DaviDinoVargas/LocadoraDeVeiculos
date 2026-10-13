using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// Status, conferência e remoção do rosto do PRÓPRIO usuário logado. O personId enviado ao
// serviço de ML é sempre derivado do e-mail no servidor (nunca vem do cliente).
[TestClass]
[TestCategory("Integração - Autenticação Facial")]
public sealed class GerenciarRostoHandlersTests
{
    private ServiceProvider _serviceProvider = null!;
    private UserManager<Usuario> _userManager = null!;

    [TestInitialize]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<LocadoraDeVeiculosDbContext>(opt =>
            opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentity<Usuario, Cargo>()
            .AddEntityFrameworkStores<LocadoraDeVeiculosDbContext>()
            .AddDefaultTokenProviders();

        _serviceProvider = services.BuildServiceProvider();
        _userManager = _serviceProvider.GetRequiredService<UserManager<Usuario>>();
    }

    private async Task<Usuario> CriarUsuarioAsync(string email)
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

        return usuario;
    }

    // A mensagem amigável mora nas "reasons" do erro (CausedBy), não no título.
    private static string Mensagens(FluentResults.ResultBase resultado) =>
        string.Join(" | ", resultado.Errors.SelectMany(e => new[] { e.Message }.Concat(e.Reasons.Select(r => r.Message))));

    // ---------- remover ----------

    [TestMethod]
    public async Task Remover_ApagaORostoUsandoPersonIdDoEmailEmMinusculas()
    {
        var usuario = await CriarUsuarioAsync("Maiuscula@Teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.RemoverAsync("usuario:maiuscula@teste.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RemoverRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<RemoverRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new RemoverRostoCommand(usuario.Id), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        mockFacial.VerifyAll();
    }

    [TestMethod]
    public async Task Remover_ComUsuarioInexistente_FalhaSemChamarOServicoDeMl()
    {
        var mockFacial = new Mock<IFacialAuthClient>(MockBehavior.Strict);
        var handler = new RemoverRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<RemoverRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new RemoverRostoCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    [TestMethod]
    public async Task Remover_QuandoServicoDeMlFalha_RetornaFalha()
    {
        var usuario = await CriarUsuarioAsync("falha@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.RemoverAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new RemoverRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<RemoverRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new RemoverRostoCommand(usuario.Id), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    [TestMethod]
    public async Task Remover_QuandoServicoDeMlEstaForaDoAr_RetornaFalhaSemLancar()
    {
        var usuario = await CriarUsuarioAsync("fora@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.RemoverAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("ml fora do ar"));

        var handler = new RemoverRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<RemoverRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new RemoverRostoCommand(usuario.Id), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    // ---------- status ----------

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Status_RepassaOQueOServicoDeMlInforma(bool cadastrado)
    {
        var usuario = await CriarUsuarioAsync("status@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.PossuiRostoAsync("usuario:status@teste.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cadastrado);

        var handler = new ObterStatusRostoQueryHandler(_userManager, mockFacial.Object, NullLogger<ObterStatusRostoQueryHandler>.Instance);

        var resultado = await handler.Handle(new ObterStatusRostoQuery(usuario.Id), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.AreEqual(cadastrado, resultado.Value);
    }

    [TestMethod]
    public async Task Status_ComUsuarioInexistente_Falha()
    {
        var mockFacial = new Mock<IFacialAuthClient>(MockBehavior.Strict);
        var handler = new ObterStatusRostoQueryHandler(_userManager, mockFacial.Object, NullLogger<ObterStatusRostoQueryHandler>.Instance);

        var resultado = await handler.Handle(new ObterStatusRostoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    // ---------- verificar ----------

    [TestMethod]
    public async Task Verificar_ComRostoQueBate_ConfirmaIdentidade()
    {
        var usuario = await CriarUsuarioAsync("bate@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.VerificarAsync("usuario:bate@teste.com", "img", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: true, Bateu: true, Confianca: 0.97));

        var handler = new VerificarRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<VerificarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new VerificarRostoCommand(usuario.Id, "img"), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.IsTrue(resultado.Value.Confirmado);
        Assert.AreEqual(0.97, resultado.Value.Confianca);
    }

    [TestMethod]
    public async Task Verificar_ComRostoQueNaoBate_RetornaSucessoNaoConfirmado()
    {
        var usuario = await CriarUsuarioAsync("naobate@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0.4));

        var handler = new VerificarRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<VerificarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new VerificarRostoCommand(usuario.Id, "img"), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.IsFalse(resultado.Value.Confirmado);
    }

    [TestMethod]
    public async Task Verificar_SemRostoCadastrado_FalhaComMensagemClara()
    {
        var usuario = await CriarUsuarioAsync("semrosto2@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: false, Bateu: false, Confianca: 0));

        var handler = new VerificarRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<VerificarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new VerificarRostoCommand(usuario.Id, "img"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        StringAssert.Contains(Mensagens(resultado), "Nenhum rosto cadastrado");
    }

    [TestMethod]
    public async Task Verificar_SemRostoNaImagem_FalhaPedindoNovaCaptura()
    {
        var usuario = await CriarUsuarioAsync("escuro@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0, NenhumRostoNaImagem: true));

        var handler = new VerificarRostoCommandHandler(_userManager, mockFacial.Object, NullLogger<VerificarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new VerificarRostoCommand(usuario.Id, "img"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        StringAssert.Contains(Mensagens(resultado), "Nenhum rosto detectado");
    }

    // ---------- auditoria (S14) ----------

    [TestMethod]
    public async Task Verificar_RegistraNaTrilha_SeOsRostosBatemOuNao()
    {
        var usuario = await CriarUsuarioAsync("audita@teste.com");
        var auditoria = new Mock<IAuditoriaDeAutenticacao>();
        var facial = new Mock<IFacialAuthClient>();
        facial.SetupSequence(f => f.VerificarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: true, Bateu: true, Confianca: 0.9, NenhumRostoNaImagem: false))
            .ReturnsAsync(new FacialVerifyResult(Encontrado: true, Bateu: false, Confianca: 0.2, NenhumRostoNaImagem: false));
        var handler = new VerificarRostoCommandHandler(_userManager, facial.Object, NullLogger<VerificarRostoCommandHandler>.Instance, auditoria.Object);

        await handler.Handle(new VerificarRostoCommand(usuario.Id, "img"), CancellationToken.None);
        await handler.Handle(new VerificarRostoCommand(usuario.Id, "img"), CancellationToken.None);

        auditoria.Verify(a => a.RegistrarAsync(EventosDeAutenticacao.RostoVerificado, It.Is<Usuario>(u => u.Id == usuario.Id), It.IsAny<CancellationToken>()), Times.Once);
        auditoria.Verify(a => a.RegistrarAsync(EventosDeAutenticacao.RostoNaoConfere, It.Is<Usuario>(u => u.Id == usuario.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Remover_RegistraNaTrilha_SoQuandoApagou()
    {
        var usuario = await CriarUsuarioAsync("apaga@teste.com");
        var auditoria = new Mock<IAuditoriaDeAutenticacao>();
        var facial = new Mock<IFacialAuthClient>();
        facial.SetupSequence(f => f.RemoverAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false).ReturnsAsync(true);
        var handler = new RemoverRostoCommandHandler(_userManager, facial.Object, NullLogger<RemoverRostoCommandHandler>.Instance, auditoria.Object);

        await handler.Handle(new RemoverRostoCommand(usuario.Id), CancellationToken.None);
        auditoria.Verify(a => a.RegistrarAsync(EventosDeAutenticacao.RostoRemovido, It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);

        await handler.Handle(new RemoverRostoCommand(usuario.Id), CancellationToken.None);
        auditoria.Verify(a => a.RegistrarAsync(EventosDeAutenticacao.RostoRemovido, It.Is<Usuario>(u => u.Id == usuario.Id), It.IsAny<CancellationToken>()), Times.Once);
    }
}
