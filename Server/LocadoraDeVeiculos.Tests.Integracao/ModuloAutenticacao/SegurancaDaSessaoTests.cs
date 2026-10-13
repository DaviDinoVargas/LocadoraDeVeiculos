using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Validators;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// S26 (desligar funcionário), S27 (troca/redefinição de senha), S29 (reuso de refresh token) e S20 (tempo de login).
[TestClass]
[TestCategory("Integração - Segurança da sessão")]
public sealed class SegurancaDaSessaoTests
{
    private sealed class Tenant(Guid empresa) : ITenantProvider
    {
        public Guid? EmpresaId => empresa;
        public bool EstaNoCargo(string cargo) => false;
    }

    private const string SenhaInicial = "Senha@12345";
    private readonly Guid _empresaId = Guid.NewGuid();
    private ServiceProvider _servicos = null!;
    private LocadoraDeVeiculosDbContext _db = null!;
    private UserManager<Usuario> _usuarios = null!;
    private RefreshTokenProvider _refresh = null!;

    [TestInitialize]
    public async Task Setup()
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddSingleton<ITenantProvider>(new Tenant(_empresaId));
        servicos.AddDbContext<LocadoraDeVeiculosDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        servicos.AddIdentity<Usuario, Cargo>().AddEntityFrameworkStores<LocadoraDeVeiculosDbContext>().AddDefaultTokenProviders();
        _servicos = servicos.BuildServiceProvider();
        _db = _servicos.GetRequiredService<LocadoraDeVeiculosDbContext>();
        _usuarios = _servicos.GetRequiredService<UserManager<Usuario>>();
        _refresh = new RefreshTokenProvider(_db, _usuarios, new HttpContextAccessor());

        var cargos = _servicos.GetRequiredService<RoleManager<Cargo>>();
        foreach (var nome in new[] { "Empresa", "Funcionario" })
            await cargos.CreateAsync(new Cargo { Id = Guid.NewGuid(), Name = nome, NormalizedName = nome.ToUpperInvariant() });

        // O dono do tenant: o repositório de funcionários faz Include da empresa (um usuário com o Id do tenant).
        var empresa = new Usuario { Id = _empresaId, UserName = "empresa@teste.com", Email = "empresa@teste.com", FullName = "Empresa" };
        await _usuarios.CreateAsync(empresa, SenhaInicial);
        await _usuarios.AddToRoleAsync(empresa, "Empresa");
    }

    [TestCleanup]
    public void Cleanup() => _servicos.Dispose();

    private async Task<Usuario> CriarUsuarioAsync(string email, string cargo = "Funcionario")
    {
        var usuario = new Usuario { UserName = email, Email = email, FullName = "Teste", AccessTokenVersionId = Guid.NewGuid() };
        var criado = await _usuarios.CreateAsync(usuario, SenhaInicial);
        Assert.IsTrue(criado.Succeeded, string.Join(";", criado.Errors.Select(e => e.Description)));
        await _usuarios.AddToRoleAsync(usuario, cargo);
        return usuario;
    }

    // ---------------------------------------------------------------- S26
    [TestMethod]
    public async Task DesligarFuncionario_CortaAcessoRevogaSessaoBloqueiaContaEApagaORosto()
    {
        var usuario = await CriarUsuarioAsync("saiu@teste.com");
        var versaoAntes = usuario.AccessTokenVersionId;
        var (_, tokenBruto) = await _refresh.GerarRefreshTokenAsync(usuario);

        var funcionario = new Funcionario(usuario.Id, _empresaId, "Saiu", "529.982.247-25", usuario.Email!, 2000m, DateTimeOffset.UtcNow);
        _db.Set<Funcionario>().Add(funcionario);
        await _db.SaveChangesAsync();

        var facial = new Mock<IFacialAuthClient>();
        facial.Setup(f => f.RemoverAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new ExcluirFuncionarioCommandHandler(
            _db, new RepositorioFuncionarioEmOrm(_db), _usuarios, new EncerradorDeAcesso(_refresh, _usuarios, facial.Object),
            NullLogger<ExcluirFuncionarioCommandHandler>.Instance);

        var resultado = await handler.Handle(new ExcluirFuncionarioCommand(funcionario.Id), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, string.Join("; ", resultado.Errors.Select(e => e.Message)));

        var depois = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.AreNotEqual(versaoAntes, depois!.AccessTokenVersionId, "o access token já emitido precisa deixar de valer");
        Assert.IsTrue(await _usuarios.IsLockedOutAsync(depois), "a conta precisa ficar bloqueada");
        Assert.IsTrue((await _db.RefreshTokens.Where(t => t.UsuarioId == usuario.Id).ToListAsync()).All(t => t.RevogadoEmUtc is not null));
        facial.Verify(f => f.RemoverAsync("usuario:saiu@teste.com", It.IsAny<CancellationToken>()), Times.Once);

        await Assert.ThrowsExceptionAsync<SecurityTokenException>(() => _refresh.RotacionarRefreshTokenAsync(tokenBruto));
    }

    // ---------------------------------------------------------------- S27
    private AlterarSenhaCommandHandler CriarHandlerDeSenha() =>
        new(_usuarios, _refresh, new AlterarSenhaCommandValidator(), NullLogger<AlterarSenhaCommandHandler>.Instance);

    [TestMethod]
    public async Task AlterarSenha_ComSenhaAtualCerta_TrocaDerrubaSessoesEFazASenhaAntigaFalhar()
    {
        var usuario = await CriarUsuarioAsync("troca@teste.com");
        var versaoAntes = usuario.AccessTokenVersionId;
        await _refresh.GerarRefreshTokenAsync(usuario);

        var resultado = await CriarHandlerDeSenha().Handle(
            new AlterarSenhaCommand(usuario.Id, SenhaInicial, "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        var depois = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.IsTrue(await _usuarios.CheckPasswordAsync(depois!, "NovaSenha@456"));
        Assert.IsFalse(await _usuarios.CheckPasswordAsync(depois!, SenhaInicial));
        Assert.AreNotEqual(versaoAntes, depois!.AccessTokenVersionId);
        Assert.IsTrue((await _db.RefreshTokens.Where(t => t.UsuarioId == usuario.Id).ToListAsync()).All(t => t.RevogadoEmUtc is not null));
    }

    [TestMethod]
    public async Task AlterarSenha_ComSenhaAtualErrada_FalhaESomaNoBloqueio()
    {
        var usuario = await CriarUsuarioAsync("errada@teste.com");

        var resultado = await CriarHandlerDeSenha().Handle(
            new AlterarSenhaCommand(usuario.Id, "Errada@99999", "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.AreEqual(1, await _usuarios.GetAccessFailedCountAsync(usuario));
        Assert.IsTrue(await _usuarios.CheckPasswordAsync(usuario, SenhaInicial), "a senha não pode ter mudado");
    }

    [TestMethod]
    public async Task AlterarSenha_CincoTentativasComSenhaAtualErrada_BloqueiamAConta()
    {
        var usuario = await CriarUsuarioAsync("forca@teste.com");
        var handler = CriarHandlerDeSenha();

        for (var i = 0; i < 6; i++)
            await handler.Handle(new AlterarSenhaCommand(usuario.Id, $"Errada@{i}9999", "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);

        var certa = await handler.Handle(new AlterarSenhaCommand(usuario.Id, SenhaInicial, "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);

        Assert.IsTrue(certa.IsFailed, "um token roubado não pode descobrir a senha por força bruta neste endpoint");
        Assert.IsTrue(await _usuarios.CheckPasswordAsync(usuario, SenhaInicial));
    }

    [TestMethod]
    [DataRow("curta1A", "curta1A")]
    [DataRow("semnumeroAAA", "semnumeroAAA")]
    [DataRow("SEMMINUSCULA1", "SEMMINUSCULA1")]
    [DataRow("NovaSenha@456", "OutraCoisa@456")]
    [DataRow(SenhaInicial, SenhaInicial)]
    public async Task AlterarSenha_ComNovaSenhaForaDaPolitica_Falha(string nova, string confirmacao)
    {
        var usuario = await CriarUsuarioAsync("politica@teste.com");

        var resultado = await CriarHandlerDeSenha().Handle(new AlterarSenhaCommand(usuario.Id, SenhaInicial, nova, confirmacao), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.IsTrue(await _usuarios.CheckPasswordAsync(usuario, SenhaInicial));
    }

    [TestMethod]
    public async Task EmpresaRedefineSenhaDoFuncionario_ComPoliticaEDerrubandoAsSessoes()
    {
        var usuario = await CriarUsuarioAsync("redef@teste.com");
        var versaoAntes = usuario.AccessTokenVersionId;
        await _refresh.GerarRefreshTokenAsync(usuario);
        var funcionario = new Funcionario(usuario.Id, _empresaId, "Redef", "529.982.247-25", usuario.Email!, 2000m, DateTimeOffset.UtcNow);
        _db.Set<Funcionario>().Add(funcionario);
        await _db.SaveChangesAsync();

        var handler = new EditarFuncionarioCommandHandler(
            _db, new RepositorioFuncionarioEmOrm(_db), new Tenant(_empresaId), _usuarios, _refresh,
            new EditarFuncionarioCommandValidator(), NullLogger<EditarFuncionarioCommandHandler>.Instance);

        var fraca = await handler.Handle(
            new EditarFuncionarioCommand(funcionario.Id, "Redef", "529.982.247-25", 2000m, DateTimeOffset.UtcNow, "fraca"), CancellationToken.None);
        Assert.IsTrue(fraca.IsFailed, "a política de senha vale para a redefinição");

        var forte = await handler.Handle(
            new EditarFuncionarioCommand(funcionario.Id, "Redef", "529.982.247-25", 2000m, DateTimeOffset.UtcNow, "Redefinida@789"), CancellationToken.None);
        Assert.IsTrue(forte.IsSuccess, string.Join("; ", forte.Errors.Select(e => e.Message)));

        var depois = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.IsTrue(await _usuarios.CheckPasswordAsync(depois!, "Redefinida@789"));
        Assert.AreNotEqual(versaoAntes, depois!.AccessTokenVersionId);
        Assert.IsTrue((await _db.RefreshTokens.Where(t => t.UsuarioId == usuario.Id).ToListAsync()).All(t => t.RevogadoEmUtc is not null));
    }

    [TestMethod]
    public async Task EditarFuncionario_SemSenha_NaoMexeNaSenha()
    {
        var usuario = await CriarUsuarioAsync("semsenha@teste.com");
        var funcionario = new Funcionario(usuario.Id, _empresaId, "Sem", "529.982.247-25", usuario.Email!, 2000m, DateTimeOffset.UtcNow);
        _db.Set<Funcionario>().Add(funcionario);
        await _db.SaveChangesAsync();

        var handler = new EditarFuncionarioCommandHandler(
            _db, new RepositorioFuncionarioEmOrm(_db), new Tenant(_empresaId), _usuarios, _refresh,
            new EditarFuncionarioCommandValidator(), NullLogger<EditarFuncionarioCommandHandler>.Instance);

        var resultado = await handler.Handle(
            new EditarFuncionarioCommand(funcionario.Id, "Novo Nome", "529.982.247-25", 3000m, DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, string.Join("; ", resultado.Errors.Select(e => e.Message)));
        Assert.IsTrue(await _usuarios.CheckPasswordAsync(usuario, SenhaInicial));
    }

    // ---------------------------------------------------------------- S29
    [TestMethod]
    public async Task ReusoDeRefreshTokenJaRotacionado_DerrubaTodasAsSessoesDoUsuario()
    {
        var usuario = await CriarUsuarioAsync("reuso@teste.com");
        var versaoAntes = usuario.AccessTokenVersionId;
        var (_, original) = await _refresh.GerarRefreshTokenAsync(usuario);

        var (_, novoToken, novoBruto) = await _refresh.RotacionarRefreshTokenAsync(original);

        // alguém reapresenta o token antigo (cookie copiado)
        await Assert.ThrowsExceptionAsync<ReusoDeRefreshTokenException>(() => _refresh.RotacionarRefreshTokenAsync(original));

        var todos = await _db.RefreshTokens.Where(t => t.UsuarioId == usuario.Id).ToListAsync();
        Assert.IsTrue(todos.All(t => t.RevogadoEmUtc is not null), "o token NOVO também precisa ter sido revogado");
        await Assert.ThrowsExceptionAsync<SecurityTokenException>(() => _refresh.RotacionarRefreshTokenAsync(novoBruto));
        Assert.AreNotEqual(versaoAntes, (await _usuarios.FindByIdAsync(usuario.Id.ToString()))!.AccessTokenVersionId);
    }

    [TestMethod]
    public async Task TokenRevogadoPorLogout_NaoDerrubaAsOutrasSessoes()
    {
        var usuario = await CriarUsuarioAsync("logout@teste.com");
        var (_, primeiro) = await _refresh.GerarRefreshTokenAsync(usuario);
        var (_, segundo) = await _refresh.GerarRefreshTokenAsync(usuario);

        var dono = await _refresh.ObterUsuarioIdPorTokenBrutoAsync(primeiro);
        var token = await _db.RefreshTokens.OrderBy(t => t.CriadoEmUtc).FirstAsync(t => t.UsuarioId == dono);
        token.RevogadoEmUtc = DateTime.UtcNow;
        token.MotivoRevogacao = "Logout";
        await _db.SaveChangesAsync();

        await Assert.ThrowsExceptionAsync<SecurityTokenException>(() => _refresh.RotacionarRefreshTokenAsync(primeiro));

        var (_, _, _) = await _refresh.RotacionarRefreshTokenAsync(segundo);
    }

    [TestMethod]
    public async Task EmitirNovoRefreshToken_ApagaTokensEncerradosHaMaisDeTrintaDias()
    {
        var usuario = await CriarUsuarioAsync("limpeza@teste.com");
        _db.RefreshTokens.Add(new RefreshToken
        {
            UsuarioId = usuario.Id, TokenHash = "velho", CriadoEmUtc = DateTime.UtcNow.AddDays(-60), ExpiraEmUtc = DateTime.UtcNow.AddDays(-53),
            IpCriacao = "203.0.113.9", UserAgent = "navegador"
        });
        await _db.SaveChangesAsync();

        await _refresh.GerarRefreshTokenAsync(usuario);

        Assert.IsFalse(await _db.RefreshTokens.AnyAsync(t => t.TokenHash == "velho"), "IP e User-Agent não ficam para sempre");
    }

    // ---------------------------------------------------------------- S20
    [TestMethod]
    public void EqualizadorDeTempo_UsaOHasherDoIdentity_ESeguraSenhaNulaOuVazia()
    {
        var hasher = _usuarios.PasswordHasher;

        EqualizadorDeTempoDeLogin.GastarCustoDeHash(hasher, "qualquer");
        EqualizadorDeTempoDeLogin.GastarCustoDeHash(hasher, "");
        EqualizadorDeTempoDeLogin.GastarCustoDeHash(hasher, null!);
    }
}
