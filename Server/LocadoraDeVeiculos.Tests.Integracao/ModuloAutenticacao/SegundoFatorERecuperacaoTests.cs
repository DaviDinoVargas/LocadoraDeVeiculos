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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

// S18: segundo fator por aplicativo autenticador (TOTP) e recuperação de senha por e-mail.
[TestClass]
[TestCategory("Integração - Segundo fator e recuperação de senha")]
public sealed class SegundoFatorERecuperacaoTests
{
    private sealed class EmailsFalsos : IEnviadorDeEmail
    {
        public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = [];

        public Task EnviarAsync(string para, string assunto, string corpo, CancellationToken cancellationToken)
        {
            Enviados.Add((para, assunto, corpo));
            return Task.CompletedTask;
        }
    }

    private const string Senha = "Senha@12345";
    private ServiceProvider _servicos = null!;
    private LocadoraDeVeiculosDbContext _db = null!;
    private UserManager<Usuario> _usuarios = null!;
    private RefreshTokenProvider _refresh = null!;

    [TestInitialize]
    public void Setup()
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddDbContext<LocadoraDeVeiculosDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        servicos.AddIdentity<Usuario, Cargo>().AddEntityFrameworkStores<LocadoraDeVeiculosDbContext>().AddDefaultTokenProviders();
        _servicos = servicos.BuildServiceProvider();
        _db = _servicos.GetRequiredService<LocadoraDeVeiculosDbContext>();
        _usuarios = _servicos.GetRequiredService<UserManager<Usuario>>();
        _refresh = new RefreshTokenProvider(_db, _usuarios, new HttpContextAccessor());
    }

    [TestCleanup]
    public void Cleanup() => _servicos.Dispose();

    private async Task<Usuario> CriarUsuarioAsync(string email)
    {
        var usuario = new Usuario { UserName = email, Email = email, FullName = "Teste", AccessTokenVersionId = Guid.NewGuid() };
        Assert.IsTrue((await _usuarios.CreateAsync(usuario, Senha)).Succeeded);
        return usuario;
    }

    // ---------------------------------------------------------------- TOTP (RFC 6238), só para o teste gerar o código do "aplicativo"
    private static byte[] Base32(string texto)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var valor = 0;
        var saida = new List<byte>();
        foreach (var c in texto.ToUpperInvariant().Where(alfabeto.Contains))
        {
            valor = (valor << 5) | alfabeto.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                saida.Add((byte)((valor >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return [.. saida];
    }

    private static string CodigoDoAplicativo(string chave, int deslocamentoDePassos = 0)
    {
        var passo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + deslocamentoDePassos;
        var contador = BitConverter.GetBytes(passo);
        if (BitConverter.IsLittleEndian) Array.Reverse(contador);
        var hash = HMACSHA1.HashData(Base32(chave), contador);
        var o = hash[^1] & 0x0F;
        var numero = ((hash[o] & 0x7F) << 24) | (hash[o + 1] << 16) | (hash[o + 2] << 8) | hash[o + 3];
        return (numero % 1_000_000).ToString("D6");
    }

    private async Task<(Usuario Usuario, string Chave)> UsuarioComDoisFatoresAsync(string email)
    {
        var usuario = await CriarUsuarioAsync(email);
        var iniciar = await new IniciarDoisFatoresCommandHandler(_usuarios).Handle(new IniciarDoisFatoresCommand(usuario.Id), CancellationToken.None);
        var chave = (await _usuarios.GetAuthenticatorKeyAsync(usuario))!;
        var ativar = await new AtivarDoisFatoresCommandHandler(_usuarios, NullLogger<AtivarDoisFatoresCommandHandler>.Instance)
            .Handle(new AtivarDoisFatoresCommand(usuario.Id, CodigoDoAplicativo(chave)), CancellationToken.None);
        Assert.IsTrue(iniciar.IsSuccess && ativar.IsSuccess, string.Join(";", ativar.Errors.Select(e => e.Message)));
        return (await _usuarios.FindByIdAsync(usuario.Id.ToString())!, chave)!;
    }

    // ---------------------------------------------------------------- ativação
    [TestMethod]
    public async Task Iniciar_DevolveChaveEUriOtpauth_SemAtivarOSegundoFator()
    {
        var usuario = await CriarUsuarioAsync("inicio@teste.com");

        var resultado = await new IniciarDoisFatoresCommandHandler(_usuarios).Handle(new IniciarDoisFatoresCommand(usuario.Id), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        StringAssert.StartsWith(resultado.Value.Uri, "otpauth://totp/");
        StringAssert.Contains(resultado.Value.Uri, "inicio%40teste.com");
        StringAssert.Contains(resultado.Value.Uri, "secret=");
        Assert.IsFalse(await _usuarios.GetTwoFactorEnabledAsync(usuario));
    }

    [TestMethod]
    public async Task Ativar_ComCodigoErrado_NaoAtiva_ComCodigoCerto_AtivaEDevolveDezCodigosDeRecuperacao()
    {
        var usuario = await CriarUsuarioAsync("ativa@teste.com");
        await new IniciarDoisFatoresCommandHandler(_usuarios).Handle(new IniciarDoisFatoresCommand(usuario.Id), CancellationToken.None);
        var chave = (await _usuarios.GetAuthenticatorKeyAsync(usuario))!;
        var handler = new AtivarDoisFatoresCommandHandler(_usuarios, NullLogger<AtivarDoisFatoresCommandHandler>.Instance);

        var errado = await handler.Handle(new AtivarDoisFatoresCommand(usuario.Id, "000000"), CancellationToken.None);
        Assert.IsTrue(errado.IsFailed || CodigoDoAplicativo(chave) == "000000");
        Assert.IsFalse(await _usuarios.GetTwoFactorEnabledAsync(usuario));

        var certo = await handler.Handle(new AtivarDoisFatoresCommand(usuario.Id, CodigoDoAplicativo(chave)), CancellationToken.None);

        Assert.IsTrue(certo.IsSuccess);
        Assert.AreEqual(10, certo.Value.Count);
        Assert.AreEqual(10, certo.Value.Distinct().Count());
        Assert.IsTrue(await _usuarios.GetTwoFactorEnabledAsync(usuario));
    }

    [TestMethod]
    public async Task Ativar_SemTerComecadoAConfiguracao_Falha()
    {
        var usuario = await CriarUsuarioAsync("seminiciar@teste.com");

        var resultado = await new AtivarDoisFatoresCommandHandler(_usuarios, NullLogger<AtivarDoisFatoresCommandHandler>.Instance)
            .Handle(new AtivarDoisFatoresCommand(usuario.Id, "123456"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.IsFalse(await _usuarios.GetTwoFactorEnabledAsync(usuario));
    }

    [TestMethod]
    public async Task Iniciar_ComSegundoFatorJaAtivo_Recusa_ParaNaoTrocarAChaveSemSenhaECodigo()
    {
        var (usuario, chave) = await UsuarioComDoisFatoresAsync("jaativo@teste.com");

        var resultado = await new IniciarDoisFatoresCommandHandler(_usuarios).Handle(new IniciarDoisFatoresCommand(usuario.Id), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.AreEqual(chave, await _usuarios.GetAuthenticatorKeyAsync(usuario), "a chave não pode ter mudado");
    }

    // ---------------------------------------------------------------- verificação no login
    [TestMethod]
    public async Task Verificador_SemSegundoFatorAtivo_Libera()
    {
        var usuario = await CriarUsuarioAsync("sem2fa@teste.com");

        Assert.AreEqual(ResultadoDoSegundoFator.Liberado, await new VerificadorDeSegundoFator(_usuarios).VerificarAsync(usuario, null));
    }

    [TestMethod]
    public async Task Verificador_ComSegundoFatorAtivo_ExigeCodigo_RecusaErrado_AceitaCerto()
    {
        var (usuario, chave) = await UsuarioComDoisFatoresAsync("com2fa@teste.com");
        var verificador = new VerificadorDeSegundoFator(_usuarios);

        Assert.AreEqual(ResultadoDoSegundoFator.CodigoObrigatorio, await verificador.VerificarAsync(usuario, null));
        Assert.AreEqual(ResultadoDoSegundoFator.CodigoObrigatorio, await verificador.VerificarAsync(usuario, "  "));

        Assert.AreEqual(ResultadoDoSegundoFator.CodigoInvalido, await verificador.VerificarAsync(usuario, "abcdef"));
        Assert.AreEqual(1, await _usuarios.GetAccessFailedCountAsync(usuario), "código errado conta no bloqueio de conta");

        var codigo = CodigoDoAplicativo(chave);
        Assert.AreEqual(ResultadoDoSegundoFator.Liberado, await verificador.VerificarAsync(usuario, codigo));
        var proximo = CodigoDoAplicativo(chave, 1);
        Assert.AreEqual(ResultadoDoSegundoFator.Liberado, await verificador.VerificarAsync(usuario, $"{proximo[..3]} {proximo[3..]}"), "aceita o formato '123 456' do aplicativo");
    }

    [TestMethod]
    public async Task Verificador_MesmoCodigoDoAplicativoDuasVezes_SegundaEhRecusadaEContaComoFalha()
    {
        var (usuario, chave) = await UsuarioComDoisFatoresAsync("replay@teste.com");
        var verificador = new VerificadorDeSegundoFator(_usuarios);
        var codigo = CodigoDoAplicativo(chave);

        Assert.AreEqual(ResultadoDoSegundoFator.Liberado, await verificador.VerificarAsync(usuario, codigo));
        Assert.AreEqual(ResultadoDoSegundoFator.CodigoInvalido, await verificador.VerificarAsync(usuario, codigo), "o mesmo código não vale duas vezes (RFC 6238, 5.2)");
        Assert.AreEqual(ResultadoDoSegundoFator.CodigoInvalido, await verificador.VerificarAsync(usuario, $"{codigo[..3]} {codigo[3..]}"), "escrito com espaço continua sendo o mesmo código");
        Assert.AreEqual(2, await _usuarios.GetAccessFailedCountAsync(usuario), "cada replay soma no bloqueio de conta");
    }

    [TestMethod]
    public void ConsumoDoCodigo_EmVariasThreadsAoMesmoTempo_SoUmaGanha()
    {
        var usuario = Guid.NewGuid();
        var ganhadores = 0;

        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 16 }, _ =>
        {
            if (VerificadorDeSegundoFator.TentarConsumirCodigo(usuario, "123456"))
                Interlocked.Increment(ref ganhadores);
        });

        Assert.AreEqual(1, ganhadores);
        Assert.IsTrue(VerificadorDeSegundoFator.TentarConsumirCodigo(Guid.NewGuid(), "123456"), "outro usuário pode usar o mesmo número");
        Assert.IsTrue(VerificadorDeSegundoFator.TentarConsumirCodigo(usuario, "654321"), "outro código do mesmo usuário também");
    }

    [TestMethod]
    public async Task Verificador_CincoCodigosErrados_BloqueiamAContaMesmoComASenhaCerta()
    {
        var (usuario, _) = await UsuarioComDoisFatoresAsync("forca2fa@teste.com");
        var verificador = new VerificadorDeSegundoFator(_usuarios);

        for (var i = 0; i < 5; i++)
            await verificador.VerificarAsync(usuario, "111111");

        Assert.IsTrue(await _usuarios.IsLockedOutAsync(usuario), "adivinhar um código de 6 dígitos tem de travar a conta em 5 tentativas");
    }

    [TestMethod]
    public async Task CodigoDeRecuperacao_FuncionaUmaVezSo()
    {
        var (usuario, _) = await UsuarioComDoisFatoresAsync("recup@teste.com");
        var codigos = (await _usuarios.GenerateNewTwoFactorRecoveryCodesAsync(usuario, 10))!.ToList();
        var verificador = new VerificadorDeSegundoFator(_usuarios);

        Assert.AreEqual(ResultadoDoSegundoFator.Liberado, await verificador.VerificarAsync(usuario, codigos[0]));
        Assert.AreEqual(ResultadoDoSegundoFator.CodigoInvalido, await verificador.VerificarAsync(usuario, codigos[0]), "o mesmo código não vale de novo");
        Assert.AreEqual(9, await _usuarios.CountRecoveryCodesAsync(usuario));
    }

    // ---------------------------------------------------------------- desativação
    [TestMethod]
    public async Task Desativar_ExigeSenhaECodigo()
    {
        var (usuario, chave) = await UsuarioComDoisFatoresAsync("desliga@teste.com");
        var handler = new DesativarDoisFatoresCommandHandler(_usuarios, NullLogger<DesativarDoisFatoresCommandHandler>.Instance);

        var semSenha = await handler.Handle(new DesativarDoisFatoresCommand(usuario.Id, "Errada@99999", CodigoDoAplicativo(chave)), CancellationToken.None);
        var semCodigo = await handler.Handle(new DesativarDoisFatoresCommand(usuario.Id, Senha, "000000"), CancellationToken.None);

        Assert.IsTrue(semSenha.IsFailed);
        Assert.IsTrue(semCodigo.IsFailed || CodigoDoAplicativo(chave) == "000000");
        Assert.IsTrue(await _usuarios.GetTwoFactorEnabledAsync(usuario), "continua ativo");

        var certo = await handler.Handle(new DesativarDoisFatoresCommand(usuario.Id, Senha, CodigoDoAplicativo(chave)), CancellationToken.None);

        Assert.IsTrue(certo.IsSuccess);
        Assert.IsFalse(await _usuarios.GetTwoFactorEnabledAsync(usuario));
    }

    // ---------------------------------------------------------------- recuperação de senha
    private RecuperacaoDeSenha CriarRecuperacao(EmailsFalsos emails) =>
        new(_usuarios, emails, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FRONTEND_URL"] = "https://app.exemplo.com/" }).Build(),
            NullLogger<RecuperacaoDeSenha>.Instance);

    [TestMethod]
    public async Task Solicitar_EnfileiraQualquerEmailComCaraDeEmail_ComRespostaIgual_ELimitaPorHora()
    {
        var fila = new FilaDeRecuperacaoDeSenha();
        var handler = new SolicitarRecuperacaoDeSenhaCommandHandler(fila);

        var existente = await handler.Handle(new SolicitarRecuperacaoDeSenhaCommand("a@b.com"), CancellationToken.None);
        var inexistente = await handler.Handle(new SolicitarRecuperacaoDeSenhaCommand("ninguem@b.com"), CancellationToken.None);
        var lixo = await handler.Handle(new SolicitarRecuperacaoDeSenhaCommand("isto-nao-e-email"), CancellationToken.None);

        Assert.IsTrue(existente.IsSuccess && inexistente.IsSuccess && lixo.IsSuccess, "a resposta nunca diferencia");
        Assert.IsTrue(fila.Leitor.TryRead(out var primeiro) && primeiro == "a@b.com");
        Assert.IsTrue(fila.Leitor.TryRead(out var segundo) && segundo == "ninguem@b.com");
        Assert.IsFalse(fila.Leitor.TryRead(out _), "texto sem @ nem entra na fila");
    }

    [TestMethod]
    public void Fila_AceitaNoMaximoTresPedidosPorHoraPorEmail_IgnorandoCaixaAlta()
    {
        var relogio = new RelogioFalso(DateTimeOffset.UtcNow);
        var fila = new FilaDeRecuperacaoDeSenha(relogio);

        Assert.IsTrue(fila.Enfileirar("vitima@b.com"));
        Assert.IsTrue(fila.Enfileirar("VITIMA@b.com"));
        Assert.IsTrue(fila.Enfileirar(" vitima@b.com "));
        Assert.IsFalse(fila.Enfileirar("vitima@b.com"), "o quarto pedido na hora é descartado: não vira disparador de spam");
        Assert.IsTrue(fila.Enfileirar("outra@b.com"), "o limite é por e-mail");

        relogio.Avancar(TimeSpan.FromMinutes(61));
        Assert.IsTrue(fila.Enfileirar("vitima@b.com"), "depois de uma hora volta a aceitar");
    }

    private sealed class RelogioFalso(DateTimeOffset inicio) : TimeProvider
    {
        private DateTimeOffset _agora = inicio;
        public override DateTimeOffset GetUtcNow() => _agora;
        public void Avancar(TimeSpan t) => _agora += t;
    }

    [TestMethod]
    public async Task Processar_ComContaExistente_EnviaEmailComLinkNoFragmentoEToken()
    {
        await CriarUsuarioAsync("existe@teste.com");
        var emails = new EmailsFalsos();

        await CriarRecuperacao(emails).ProcessarPedidoAsync("EXISTE@teste.com", CancellationToken.None);

        Assert.AreEqual(1, emails.Enviados.Count);
        Assert.AreEqual("existe@teste.com", emails.Enviados[0].Para);
        StringAssert.Contains(emails.Enviados[0].Corpo, "https://app.exemplo.com/redefinir-senha#email=existe%40teste.com&token=");
        Assert.IsFalse(emails.Enviados[0].Corpo.Contains("redefinir-senha?"), "o token vai no fragmento, nunca na query");
    }

    [TestMethod]
    public async Task Processar_SemContaOuComContaEncerrada_NaoEnviaNada()
    {
        var emails = new EmailsFalsos();
        var recuperacao = CriarRecuperacao(emails);
        await recuperacao.ProcessarPedidoAsync("ninguem@teste.com", CancellationToken.None);

        var encerrada = await CriarUsuarioAsync("encerrada@teste.com");
        await _usuarios.SetLockoutEnabledAsync(encerrada, true);
        await _usuarios.SetLockoutEndDateAsync(encerrada, DateTimeOffset.MaxValue);
        await recuperacao.ProcessarPedidoAsync("encerrada@teste.com", CancellationToken.None);

        Assert.AreEqual(0, emails.Enviados.Count);
    }

    private static string TokenDoLink(string corpo) => Regex.Match(corpo, @"token=([A-Za-z0-9_\-]+)").Groups[1].Value;

    private RedefinirSenhaCommandHandler CriarRedefinicao() =>
        new(_usuarios, _refresh, NullLogger<RedefinirSenhaCommandHandler>.Instance);

    [TestMethod]
    public async Task Redefinir_ComOTokenDoEmail_TrocaASenha_DerrubaSessoes_EDestravaAConta()
    {
        var usuario = await CriarUsuarioAsync("redefine@teste.com");
        await _refresh.GerarRefreshTokenAsync(usuario);
        for (var i = 0; i < 5; i++) await _usuarios.AccessFailedAsync(usuario);
        Assert.IsTrue(await _usuarios.IsLockedOutAsync(usuario));
        var versaoAntes = usuario.AccessTokenVersionId;
        var emails = new EmailsFalsos();
        await CriarRecuperacao(emails).ProcessarPedidoAsync("redefine@teste.com", CancellationToken.None);

        var resultado = await CriarRedefinicao().Handle(
            new RedefinirSenhaCommand("redefine@teste.com", TokenDoLink(emails.Enviados[0].Corpo), "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess, string.Join(";", resultado.Errors.Select(e => e.Message)));
        var depois = await _usuarios.FindByIdAsync(usuario.Id.ToString());
        Assert.IsTrue(await _usuarios.CheckPasswordAsync(depois!, "NovaSenha@456"));
        Assert.IsFalse(await _usuarios.CheckPasswordAsync(depois!, Senha));
        Assert.IsFalse(await _usuarios.IsLockedOutAsync(depois!), "provar que lê o e-mail destrava o bloqueio por tentativas");
        Assert.AreNotEqual(versaoAntes, depois!.AccessTokenVersionId);
        Assert.IsTrue((await _db.RefreshTokens.Where(t => t.UsuarioId == usuario.Id).ToListAsync()).All(t => t.RevogadoEmUtc is not null));
    }

    [TestMethod]
    public async Task Redefinir_OTokenSoValeUmaVez()
    {
        await CriarUsuarioAsync("umavez@teste.com");
        var emails = new EmailsFalsos();
        await CriarRecuperacao(emails).ProcessarPedidoAsync("umavez@teste.com", CancellationToken.None);
        var token = TokenDoLink(emails.Enviados[0].Corpo);

        var primeira = await CriarRedefinicao().Handle(new RedefinirSenhaCommand("umavez@teste.com", token, "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);
        var segunda = await CriarRedefinicao().Handle(new RedefinirSenhaCommand("umavez@teste.com", token, "OutraSenha@789", "OutraSenha@789"), CancellationToken.None);

        Assert.IsTrue(primeira.IsSuccess);
        Assert.IsTrue(segunda.IsFailed, "um link usado não pode trocar a senha de novo");
    }

    [TestMethod]
    [DataRow("naoexiste@teste.com", "qualquer")]
    [DataRow("redefine2@teste.com", "token-inventado")]
    [DataRow("redefine2@teste.com", "")]
    [DataRow("", "x")]
    public async Task Redefinir_ComTokenOuEmailInvalido_DaSempreAMesmaMensagem(string email, string token)
    {
        await CriarUsuarioAsync("redefine2@teste.com");

        var resultado = await CriarRedefinicao().Handle(new RedefinirSenhaCommand(email, token, "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        StringAssert.Contains(string.Join(" ", resultado.Errors.SelectMany(e => e.Reasons).Select(r => r.Message).Concat(resultado.Errors.Select(e => e.Message))), "Link inválido ou expirado");
    }

    [TestMethod]
    public async Task Redefinir_SenhaForaDaPolitica_FalhaSemGastarOToken_E_ContaEncerradaNaoReativa()
    {
        var usuario = await CriarUsuarioAsync("politica2@teste.com");
        var emails = new EmailsFalsos();
        await CriarRecuperacao(emails).ProcessarPedidoAsync("politica2@teste.com", CancellationToken.None);
        var token = TokenDoLink(emails.Enviados[0].Corpo);

        var fraca = await CriarRedefinicao().Handle(new RedefinirSenhaCommand("politica2@teste.com", token, "fraca", "fraca"), CancellationToken.None);
        Assert.IsTrue(fraca.IsFailed);

        var certa = await CriarRedefinicao().Handle(new RedefinirSenhaCommand("politica2@teste.com", token, "NovaSenha@456", "NovaSenha@456"), CancellationToken.None);
        Assert.IsTrue(certa.IsSuccess, "o token não foi consumido pela tentativa recusada pela política");

        await _usuarios.SetLockoutEnabledAsync(usuario, true);
        await _usuarios.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);
        var novoToken = RecuperacaoDeSenha.CodificarToken(await _usuarios.GeneratePasswordResetTokenAsync(usuario));
        var encerrada = await CriarRedefinicao().Handle(new RedefinirSenhaCommand("politica2@teste.com", novoToken, "Terceira@12345", "Terceira@12345"), CancellationToken.None);

        Assert.IsTrue(encerrada.IsFailed, "conta encerrada nunca é reativada por redefinição de senha");
        Assert.IsTrue(await _usuarios.IsLockedOutAsync((await _usuarios.FindByIdAsync(usuario.Id.ToString()))!));
    }

    [TestMethod]
    public void Token_CodificaEDecodificaSemPerda_ERecusaLixo()
    {
        var original = "CfDJ8N+/abc==token/com+simbolos";

        Assert.AreEqual(original, RecuperacaoDeSenha.DecodificarToken(RecuperacaoDeSenha.CodificarToken(original)));
        Assert.IsNull(RecuperacaoDeSenha.DecodificarToken("***"));
        Assert.IsNull(RecuperacaoDeSenha.DecodificarToken(null));
    }
}
