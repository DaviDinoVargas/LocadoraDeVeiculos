using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using Moq;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;
using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;
using LocadoraDeVeiculos.Infraestrutura.Orm.jwt.Services;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloFuncionario;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// S1: consentimento registrado, retenção de 12 meses sem uso e encerramento de conta com apagamento da biometria.
[TestClass]
[TestCategory("Integração - Consentimento e retenção do rosto")]
public sealed class ConsentimentoERetencaoTests
{
    private sealed class Tenant(Guid empresa) : ITenantProvider
    {
        public Guid? EmpresaId => empresa;
        public bool EstaNoCargo(string cargo) => false;
    }

    private const string Senha = "Senha@12345";
    private readonly Guid _empresaId = Guid.NewGuid();
    private ServiceProvider _servicos = null!;
    private LocadoraDeVeiculosDbContext _db = null!;
    private UserManager<Usuario> _usuarios = null!;
    private RefreshTokenProvider _refresh = null!;
    private ArmazenamentoFacialEmOrm _armazenamento = null!;

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
        _armazenamento = new ArmazenamentoFacialEmOrm(_db, new CifradorFacial(new Dictionary<byte, byte[]> { [1] = new byte[32] }));

        var cargos = _servicos.GetRequiredService<RoleManager<Cargo>>();
        foreach (var nome in new[] { "Empresa", "Funcionario" })
            await cargos.CreateAsync(new Cargo { Id = Guid.NewGuid(), Name = nome, NormalizedName = nome.ToUpperInvariant() });
    }

    [TestCleanup]
    public void Cleanup() => _servicos.Dispose();

    private async Task<Usuario> CriarUsuarioAsync(string email, string cargo, Guid? id = null)
    {
        var usuario = new Usuario { UserName = email, Email = email, FullName = "Teste", AccessTokenVersionId = Guid.NewGuid() };
        if (id is not null) usuario.Id = id.Value;
        var criado = await _usuarios.CreateAsync(usuario, Senha);
        Assert.IsTrue(criado.Succeeded);
        await _usuarios.AddToRoleAsync(usuario, cargo);
        return usuario;
    }

    private async Task DefinirUltimoUsoAsync(string pessoa, DateTime quando)
    {
        foreach (var amostra in await _db.Set<AmostraFacial>().Where(a => a.PessoaId == pessoa).ToListAsync())
            amostra.UltimoUsoEmUtc = quando;
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------ consentimento e uso
    [TestMethod]
    public async Task Consentimento_FicaRegistradoComAVersao_ERevogarOApagarJuntoComORosto()
    {
        await _armazenamento.SalvarAsync("usuario:a@b.com", [0.1, 0.2], CancellationToken.None);
        await _armazenamento.RegistrarConsentimentoAsync("usuario:A@b.com", "2026-10-v1", CancellationToken.None);

        Assert.AreEqual("2026-10-v1", await _armazenamento.ObterVersaoDoConsentimentoAsync("usuario:a@b.com", CancellationToken.None));

        await _armazenamento.RegistrarConsentimentoAsync("usuario:a@b.com", "2027-01-v2", CancellationToken.None);
        Assert.AreEqual(1, await _db.Set<ConsentimentoFacial>().CountAsync(), "aceitar de novo renova o registro, não duplica");
        Assert.AreEqual("2027-01-v2", await _armazenamento.ObterVersaoDoConsentimentoAsync("usuario:a@b.com", CancellationToken.None));

        Assert.IsTrue(await _armazenamento.RemoverAsync("usuario:a@b.com", CancellationToken.None));
        Assert.IsNull(await _armazenamento.ObterVersaoDoConsentimentoAsync("usuario:a@b.com", CancellationToken.None));
        Assert.AreEqual(0, await _db.Set<AmostraFacial>().CountAsync());
    }

    [TestMethod]
    public async Task RegistrarUso_RenovaAContagemDeRetencaoDeTodasAsAmostras()
    {
        await _armazenamento.SalvarAsync("usuario:a@b.com", [1], CancellationToken.None);
        await _armazenamento.SalvarAsync("usuario:a@b.com", [2], CancellationToken.None);
        await DefinirUltimoUsoAsync("usuario:a@b.com", DateTime.UtcNow.AddDays(-300));

        await _armazenamento.RegistrarUsoAsync("usuario:a@b.com", CancellationToken.None);

        var amostras = await _db.Set<AmostraFacial>().AsNoTracking().ToListAsync();
        Assert.IsTrue(amostras.All(a => (DateTime.UtcNow - a.UltimoUsoEmUtc).TotalMinutes < 1));
    }

    // ------------------------------------------------------------ retenção
    [TestMethod]
    public async Task Limpeza_ApagaORostoSemUsoHaMaisDeUmAno_ComOConsentimento_EPreservaQuemUsouRecentemente()
    {
        await _armazenamento.SalvarAsync("usuario:velho@b.com", [1], CancellationToken.None);
        await _armazenamento.RegistrarConsentimentoAsync("usuario:velho@b.com", "v1", CancellationToken.None);
        await _armazenamento.SalvarAsync("usuario:ativo@b.com", [2], CancellationToken.None);
        await _armazenamento.RegistrarConsentimentoAsync("usuario:ativo@b.com", "v1", CancellationToken.None);
        await DefinirUltimoUsoAsync("usuario:velho@b.com", DateTime.UtcNow.AddDays(-366));
        await DefinirUltimoUsoAsync("usuario:ativo@b.com", DateTime.UtcNow.AddDays(-364));

        var resultado = await new LimpezaDeDadosPessoais(_db).ExecutarAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.AreEqual(1, resultado.AmostrasFaciais);
        Assert.AreEqual(1, resultado.Consentimentos);
        Assert.IsFalse(await _armazenamento.ExisteAsync("usuario:velho@b.com", CancellationToken.None));
        Assert.IsNull(await _armazenamento.ObterVersaoDoConsentimentoAsync("usuario:velho@b.com", CancellationToken.None));
        Assert.IsTrue(await _armazenamento.ExisteAsync("usuario:ativo@b.com", CancellationToken.None));
        Assert.AreEqual("v1", await _armazenamento.ObterVersaoDoConsentimentoAsync("usuario:ativo@b.com", CancellationToken.None));
    }

    [TestMethod]
    public async Task Limpeza_ApagaConsentimentoQueFicouSemRosto()
    {
        await _armazenamento.RegistrarConsentimentoAsync("usuario:orfao@b.com", "v1", CancellationToken.None);

        var resultado = await new LimpezaDeDadosPessoais(_db).ExecutarAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.AreEqual(1, resultado.Consentimentos);
        Assert.AreEqual(0, await _db.Set<ConsentimentoFacial>().CountAsync());
    }

    [TestMethod]
    public async Task Limpeza_AnonimizaIpAntigoDaAuditoria_MantendoOEvento_E_ApagaTokensVelhos()
    {
        var empresa = Guid.NewGuid();
        _db.Set<RegistroDeAuditoria>().AddRange(
            new RegistroDeAuditoria { EmpresaId = empresa, Acao = "LoginOk", Entidade = "Autenticacao", EnderecoIp = "203.0.113.1", EmUtc = DateTime.UtcNow.AddDays(-181) },
            new RegistroDeAuditoria { EmpresaId = empresa, Acao = "LoginOk", Entidade = "Autenticacao", EnderecoIp = "203.0.113.2", EmUtc = DateTime.UtcNow.AddDays(-10) });
        var usuario = await CriarUsuarioAsync("tokens@b.com", "Empresa");
        _db.RefreshTokens.AddRange(
            new RefreshToken { UsuarioId = usuario.Id, TokenHash = "vencido-velho", ExpiraEmUtc = DateTime.UtcNow.AddDays(-40), IpCriacao = "203.0.113.3", UserAgent = "x" },
            new RefreshToken { UsuarioId = usuario.Id, TokenHash = "revogado-velho", ExpiraEmUtc = DateTime.UtcNow.AddDays(5), RevogadoEmUtc = DateTime.UtcNow.AddDays(-31) },
            new RefreshToken { UsuarioId = usuario.Id, TokenHash = "ativo", ExpiraEmUtc = DateTime.UtcNow.AddDays(5) });
        await _db.SaveChangesAsync();

        var resultado = await new LimpezaDeDadosPessoais(_db).ExecutarAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.AreEqual(1, resultado.EnderecosIpAnonimizados);
        Assert.AreEqual(2, resultado.RefreshTokens);
        var registros = await _db.Set<RegistroDeAuditoria>().IgnoreQueryFilters().ToListAsync();
        Assert.AreEqual(2, registros.Count, "o evento continua na trilha");
        Assert.AreEqual(1, registros.Count(r => r.EnderecoIp is null), "só o IP do registro antigo some");
        CollectionAssert.AreEqual(new[] { "ativo" }, await _db.RefreshTokens.Select(t => t.TokenHash).ToListAsync());
    }

    [TestMethod]
    public async Task Limpeza_EhIdempotente_ESemNadaParaApagarNaoMexeEmNada()
    {
        await _armazenamento.SalvarAsync("usuario:a@b.com", [1], CancellationToken.None);
        var limpeza = new LimpezaDeDadosPessoais(_db);

        var primeira = await limpeza.ExecutarAsync(DateTime.UtcNow, CancellationToken.None);
        var segunda = await limpeza.ExecutarAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.AreEqual(new ResultadoDaLimpeza(0, 0, 0, 0), primeira);
        Assert.AreEqual(primeira, segunda);
        Assert.IsTrue(await _armazenamento.ExisteAsync("usuario:a@b.com", CancellationToken.None));
    }

    // ------------------------------------------------------------ encerrar conta
    private EncerrarContaCommandHandler CriarHandlerDeEncerramento(IFacialAuthClient facial) =>
        new(_usuarios, new RepositorioFuncionarioEmOrm(_db), new EncerradorDeAcesso(_refresh, _usuarios, facial),
            NullLogger<EncerrarContaCommandHandler>.Instance);

    [TestMethod]
    public async Task EncerrarConta_BloqueiaAEmpresaEOsFuncionarios_DerrubaSessoesEApagaToda_AsBiometrias()
    {
        var empresa = await CriarUsuarioAsync("empresa@b.com", "Empresa", _empresaId);
        var f1 = await CriarUsuarioAsync("f1@b.com", "Funcionario");
        var f2 = await CriarUsuarioAsync("f2@b.com", "Funcionario");
        _db.Set<Funcionario>().AddRange(
            new Funcionario(f1.Id, _empresaId, "F1", "529.982.247-25", f1.Email!, 1000m, DateTimeOffset.UtcNow),
            new Funcionario(f2.Id, _empresaId, "F2", "168.995.350-09", f2.Email!, 1000m, DateTimeOffset.UtcNow));
        await _db.SaveChangesAsync();
        foreach (var u in new[] { empresa, f1, f2 })
        {
            await _refresh.GerarRefreshTokenAsync(u);
            await _armazenamento.SalvarAsync(FacialAuthClient.PersonIdParaEmail(u.Email!), [1], CancellationToken.None);
            await _armazenamento.RegistrarConsentimentoAsync(FacialAuthClient.PersonIdParaEmail(u.Email!), "v1", CancellationToken.None);
        }

        var facial = new Moq.Mock<IFacialAuthClient>();
        facial.Setup(f => f.RemoverAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((pessoa, ct) => _armazenamento.RemoverAsync(pessoa, ct).ContinueWith(_ => true));

        var resultado = await CriarHandlerDeEncerramento(facial.Object).Handle(new EncerrarContaCommand(empresa.Id, Senha), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, string.Join("; ", resultado.Errors.Select(e => e.Message)));
        foreach (var id in new[] { empresa.Id, f1.Id, f2.Id })
        {
            var depois = await _usuarios.FindByIdAsync(id.ToString());
            Assert.IsTrue(EncerradorDeAcesso.ContaEncerrada(depois!), "conta bloqueada para sempre");
            Assert.IsTrue((await _db.RefreshTokens.Where(t => t.UsuarioId == id).ToListAsync()).All(t => t.RevogadoEmUtc is not null));
        }
        Assert.AreEqual(0, await _db.Set<AmostraFacial>().CountAsync(), "nenhuma biometria sobra");
        Assert.AreEqual(0, await _db.Set<ConsentimentoFacial>().CountAsync(), "nenhum consentimento sobra");
    }

    [TestMethod]
    public async Task EncerrarConta_ComSenhaErrada_NaoMudaNada_ESomaNoBloqueio()
    {
        var empresa = await CriarUsuarioAsync("empresa2@b.com", "Empresa", _empresaId);
        var facial = new Moq.Mock<IFacialAuthClient>(Moq.MockBehavior.Strict);

        var resultado = await CriarHandlerDeEncerramento(facial.Object).Handle(new EncerrarContaCommand(empresa.Id, "Errada@99999"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.IsFalse(EncerradorDeAcesso.ContaEncerrada((await _usuarios.FindByIdAsync(empresa.Id.ToString()))!));
        Assert.AreEqual(1, await _usuarios.GetAccessFailedCountAsync(empresa));
    }

    [TestMethod]
    public async Task ContaEncerrada_NaoPodeSerReativadaPelaSenha()
    {
        var empresa = await CriarUsuarioAsync("empresa3@b.com", "Empresa", _empresaId);
        var facial = new Moq.Mock<IFacialAuthClient>();
        facial.Setup(f => f.RemoverAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CriarHandlerDeEncerramento(facial.Object).Handle(new EncerrarContaCommand(empresa.Id, Senha), CancellationToken.None);

        var depois = await _usuarios.FindByIdAsync(empresa.Id.ToString());
        Assert.IsTrue(await _usuarios.IsLockedOutAsync(depois!));
        Assert.IsTrue(EncerradorDeAcesso.ContaEncerrada(depois!));
    }
}
