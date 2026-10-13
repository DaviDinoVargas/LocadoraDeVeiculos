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
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// Integração porque usa UserManager<Usuario> de verdade (Identity + EF InMemory);
// só o serviço de ML (chamada HTTP externa) é mockado, já que ele não é o que
// este teste quer validar.
[TestClass]
[TestCategory("Integração - Autenticação Facial")]
public sealed class CadastrarRostoCommandHandlerTests
{
    private readonly Mock<IArmazenamentoFacial> _armazenamento = new();
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
            throw new Exception("Falha ao criar usuário de teste para cadastro de rosto.");

        return usuario;
    }

    [TestMethod]
    public async Task Handle_ComUsuarioValido_CadastraRostoUsandoPersonIdDoEmail()
    {
        var usuario = await CriarUsuarioAsync("rosto@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.CadastrarAsync("usuario:rosto@teste.com", "imagem-base64", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialEnrollResult(Sucesso: true, Amostras: 2));

        var handler = new CadastrarRostoCommandHandler(_userManager, mockFacial.Object, _armazenamento.Object, NullLogger<CadastrarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new CadastrarRostoCommand(usuario.Id, "imagem-base64", true, ConsentimentoBiometrico.VersaoAtual), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        Assert.AreEqual(2, resultado.Value);
        mockFacial.VerifyAll();
    }

    [TestMethod]
    public async Task Handle_ComUsuarioInexistente_FalhaSemChamarServicoDeMl()
    {
        var mockFacial = new Mock<IFacialAuthClient>(MockBehavior.Strict);
        var handler = new CadastrarRostoCommandHandler(_userManager, mockFacial.Object, _armazenamento.Object, NullLogger<CadastrarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new CadastrarRostoCommand(Guid.NewGuid(), "imagem-base64", true, ConsentimentoBiometrico.VersaoAtual), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        // MockBehavior.Strict faria o teste falhar com MockException se qualquer método
        // do IFacialAuthClient fosse chamado sem expectativa configurada -- ou seja, o
        // handler não pode "vazar" uma chamada ao serviço de ML pra um usuário que não existe.
    }

    [TestMethod]
    public async Task Handle_QuandoServicoDeMlRecusaCadastro_RetornaFalhaComMensagemAmigavel()
    {
        var usuario = await CriarUsuarioAsync("semrosto@teste.com");

        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial
            .Setup(f => f.CadastrarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialEnrollResult(Sucesso: false, Amostras: 0));

        var handler = new CadastrarRostoCommandHandler(_userManager, mockFacial.Object, _armazenamento.Object, NullLogger<CadastrarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(new CadastrarRostoCommand(usuario.Id, "imagem-sem-rosto", true, ConsentimentoBiometrico.VersaoAtual), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
    }

    [TestMethod]
    public async Task Handle_SemConsentimento_FalhaSemChamarMlNemGuardarNada()
    {
        var usuario = await CriarUsuarioAsync("semaceite@teste.com");
        var mockFacial = new Mock<IFacialAuthClient>(MockBehavior.Strict);
        var handler = new CadastrarRostoCommandHandler(_userManager, mockFacial.Object, _armazenamento.Object, NullLogger<CadastrarRostoCommandHandler>.Instance);

        var semAceite = await handler.Handle(new CadastrarRostoCommand(usuario.Id, "img", false, ConsentimentoBiometrico.VersaoAtual), CancellationToken.None);
        var semVersao = await handler.Handle(new CadastrarRostoCommand(usuario.Id, "img", true, null), CancellationToken.None);
        var versaoVelha = await handler.Handle(new CadastrarRostoCommand(usuario.Id, "img", true, "1999-01-v0"), CancellationToken.None);

        Assert.IsTrue(semAceite.IsFailed && semVersao.IsFailed && versaoVelha.IsFailed);
        _armazenamento.Verify(a => a.RegistrarConsentimentoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Handle_ComConsentimento_RegistraAVersaoAceitaDepoisDoCadastro()
    {
        var usuario = await CriarUsuarioAsync("aceitou@teste.com");
        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial.Setup(f => f.CadastrarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialEnrollResult(true, 1));
        var handler = new CadastrarRostoCommandHandler(_userManager, mockFacial.Object, _armazenamento.Object, NullLogger<CadastrarRostoCommandHandler>.Instance);

        var resultado = await handler.Handle(
            new CadastrarRostoCommand(usuario.Id, "img", true, ConsentimentoBiometrico.VersaoAtual), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        _armazenamento.Verify(a => a.RegistrarConsentimentoAsync("usuario:aceitou@teste.com", ConsentimentoBiometrico.VersaoAtual, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Handle_QuandoOMlRecusaACaptura_NaoRegistraConsentimento()
    {
        var usuario = await CriarUsuarioAsync("recusado@teste.com");
        var mockFacial = new Mock<IFacialAuthClient>();
        mockFacial.Setup(f => f.CadastrarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacialEnrollResult(false, 0));
        var handler = new CadastrarRostoCommandHandler(_userManager, mockFacial.Object, _armazenamento.Object, NullLogger<CadastrarRostoCommandHandler>.Instance);

        await handler.Handle(new CadastrarRostoCommand(usuario.Id, "img", true, ConsentimentoBiometrico.VersaoAtual), CancellationToken.None);

        _armazenamento.Verify(a => a.RegistrarConsentimentoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
