using System.Diagnostics;

namespace LocadoraDeVeiculos.Testes.Unidade.Seguranca;

// Dado biométrico (landmarks faciais), recortes de placas e pesos de modelo nunca podem ser versionados.
// Falha se algum arquivo desse tipo estiver rastreado pelo git.
[TestClass]
[TestCategory("Unidade - Biometria fora do git")]
public sealed class BiometriaForaDoGitTests
{
    private static readonly string[] PadroesProibidos =
    [
        "faces.json", "FaceAuth/data/", ".faces", "/crops/", ".pt", ".pth", ".pfx",
        // resultados dos testes de ataque descrevem falhas abertas: ficam fora do git
        "Seguranca-Resultados", "Testes-Seguranca/resultados", "Testes-Seguranca/resultado.json"
    ];

    private static string? RaizDoRepositorio()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
            if (Directory.Exists(Path.Combine(pasta.FullName, ".git")) || File.Exists(Path.Combine(pasta.FullName, ".git")))
                return pasta.FullName;

        return null;
    }

    private static List<string>? ArquivosRastreados(string raiz)
    {
        try
        {
            using var processo = Process.Start(new ProcessStartInfo("git", "ls-files")
            {
                WorkingDirectory = raiz,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            if (processo is null)
                return null;

            var saida = processo.StandardOutput.ReadToEnd();
            processo.WaitForExit();

            return processo.ExitCode == 0
                ? saida.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    [TestMethod]
    public void NenhumArquivoBiometricoOuSensivelEstaRastreadoPeloGit()
    {
        var raiz = RaizDoRepositorio();
        var arquivos = raiz is null ? null : ArquivosRastreados(raiz);

        if (arquivos is null)
            Assert.Inconclusive("Repositório git ou executável git indisponível neste ambiente.");

        var proibidos = arquivos
            .Where(a => PadroesProibidos.Any(p => a.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.AreEqual(0, proibidos.Count, "Arquivos que não podem ir para o git: " + string.Join(", ", proibidos));
    }

    [TestMethod]
    public void GitignoreCobreOsCaminhosDeBiometria()
    {
        var raiz = RaizDoRepositorio();

        if (raiz is null)
            Assert.Inconclusive("Repositório git indisponível neste ambiente.");

        var gitignore = File.ReadAllText(Path.Combine(raiz, ".gitignore"));

        foreach (var regra in new[] { "faces.json", "MachineLeaning/FaceAuth/data/", "*.pt", "MachineLeaning/**/crops/", "Seguranca-Resultados/" })
            StringAssert.Contains(gitignore, regra, $".gitignore precisa conter \"{regra}\".");
    }
}
