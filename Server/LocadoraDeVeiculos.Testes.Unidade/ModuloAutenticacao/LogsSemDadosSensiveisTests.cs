using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Handlers;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.RegularExpressions;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAutenticacao;

// S22 / S11: uma exceção inesperada no login não pode levar a senha (nem o e-mail inteiro) para o log.
[TestClass]
[TestCategory("Unidade - Segurança de logs")]
public sealed class LogsSemDadosSensiveisTests
{
    private sealed class LoggerCapturador<T> : ILogger<T>
    {
        public List<string> Linhas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Linhas.Add(formatter(state, exception));

            // Os argumentos do template também contam: um sink estruturado serializaria cada um.
            if (state is IEnumerable<KeyValuePair<string, object?>> pares)
                Linhas.AddRange(pares.Select(p => p.Value?.ToString() ?? string.Empty));
        }
    }

    private static UserManager<Usuario> UserManagerQueFalha()
    {
        var store = new Mock<IUserStore<Usuario>>();
        var userManager = new Mock<UserManager<Usuario>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        userManager
            .Setup(u => u.FindByEmailAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("banco fora do ar"));

        return userManager.Object;
    }

    [TestMethod]
    public async Task Autenticar_ComExcecaoInesperada_NaoGravaASenhaNoLog()
    {
        const string senha = "S3nh@-Super-Secreta-123";
        var logger = new LoggerCapturador<AutenticarUsuarioCommandHandler>();
        var handler = new AutenticarUsuarioCommandHandler(UserManagerQueFalha(), null!, null!, null!, logger);

        var resultado = await handler.Handle(
            new AutenticarUsuarioCommand("pessoa@exemplo.com", senha), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.IsTrue(logger.Linhas.Count > 0, "o erro deveria ter sido registrado");
        Assert.IsFalse(logger.Linhas.Any(l => l.Contains(senha)), "a senha apareceu no log");
    }

    [TestMethod]
    public async Task Registrar_ComExcecaoInesperada_NaoGravaASenhaNoLog()
    {
        const string senha = "S3nh@-Super-Secreta-123";
        var logger = new LoggerCapturador<RegistrarUsuarioCommandHandler>();
        var userManager = new Mock<UserManager<Usuario>>(
            new Mock<IUserStore<Usuario>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
        userManager
            .Setup(u => u.CreateAsync(It.IsAny<Usuario>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("banco fora do ar"));

        var handler = new RegistrarUsuarioCommandHandler(userManager.Object, null!, null!, null!, logger);

        var resultado = await handler.Handle(
            new RegistrarUsuarioCommand("Pessoa", "pessoa@exemplo.com", senha, senha), CancellationToken.None);

        Assert.IsTrue(resultado.IsFailed);
        Assert.IsFalse(logger.Linhas.Any(l => l.Contains(senha)), "a senha apareceu no log");
    }

    [TestMethod]
    public void Handlers_NaoDestruturamComandosNoLog()
    {
        // Rede de proteção para todos os módulos: `{@Command}`/`{@Query}` desestrutura o objeto inteiro
        // (CPF, CNPJ, senha, imagem) e é exatamente o padrão que causou o S22.
        var raiz = LocalizarPasta("LocadoraDeVeiculos.Core.Aplicacao");
        var padrao = new Regex(@"\{@(Command|Query)\}");

        var infratores = Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => padrao.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.AreEqual(0, infratores.Count, "Comando inteiro no log em: " + string.Join(", ", infratores));
    }

    [TestMethod]
    [DataRow("pessoa@exemplo.com", "p***@exemplo.com")]
    [DataRow("a@b.com", "a***@b.com")]
    [DataRow("sem-arroba", "***")]
    [DataRow("@dominio.com", "***")]
    [DataRow("", "(vazio)")]
    [DataRow(null, "(vazio)")]
    public void MascararEmail_EscondeAParteLocal(string? entrada, string esperado)
    {
        Assert.AreEqual(esperado, MascaramentoDeDados.MascararEmail(entrada));
    }

    private static string LocalizarPasta(string nome)
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (diretorio is not null)
        {
            var candidato = Path.Combine(diretorio.FullName, nome);
            if (Directory.Exists(candidato))
                return candidato;
            diretorio = diretorio.Parent;
        }

        throw new DirectoryNotFoundException(nome);
    }
}
