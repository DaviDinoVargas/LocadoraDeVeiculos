using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using System.Text.Json;

namespace LocadoraDeVeiculos.WebApi.Config.Orm;

/// <summary>
/// Migração única (S1): leva o antigo <c>faces.json</c> do serviço de ML para o banco, já cifrado.
/// Roda como comando da própria API (<c>dotnet run --project Server/web-api -- --importar-faces &lt;arquivo&gt;
/// [--apagar-origem]</c>), sem endpoint HTTP, para que não exista uma porta de entrada para biometria.
/// É idempotente: quem já tem amostras no banco é ignorado.
/// </summary>
public static class ImportadorDeFaces
{
    public const string Argumento = "--importar-faces";
    public const string ArgumentoApagarOrigem = "--apagar-origem";

    public static bool FoiSolicitado(string[] args) => args.Contains(Argumento);

    public static string? CaminhoDoArquivo(string[] args)
    {
        var indice = Array.IndexOf(args, Argumento);

        return indice >= 0 && indice + 1 < args.Length && !args[indice + 1].StartsWith("--") ? args[indice + 1] : null;
    }

    public static async Task<int> ExecutarAsync(IServiceProvider servicos, string[] args, TextWriter saida)
    {
        var caminho = CaminhoDoArquivo(args);

        if (caminho is null)
        {
            await saida.WriteLineAsync($"Uso: {Argumento} <caminho do faces.json> [{ArgumentoApagarOrigem}]");
            return 2;
        }

        if (!File.Exists(caminho))
        {
            await saida.WriteLineAsync($"Arquivo não encontrado: {caminho}");
            return 2;
        }

        using var escopo = servicos.CreateScope();
        var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoFacial>();

        var (importadas, ignoradas) = await ImportarAsync(File.ReadAllText(caminho), armazenamento, CancellationToken.None);

        await saida.WriteLineAsync($"Pessoas importadas: {importadas}. Já existentes no banco (ignoradas): {ignoradas}.");

        if (args.Contains(ArgumentoApagarOrigem))
        {
            File.Delete(caminho);
            await saida.WriteLineAsync($"Arquivo de origem apagado: {caminho}");
        }
        else
        {
            await saida.WriteLineAsync("O arquivo de origem NÃO foi apagado (biometria em claro). Apague-o, ou rode de novo com " + ArgumentoApagarOrigem + ".");
        }

        return 0;
    }

    /// <summary>Formato antigo: { "personId": [ [ {x,y,z}, ... ], ... ] }. Cada amostra vira [x0,y0,z0,x1,...].</summary>
    public static async Task<(int Importadas, int Ignoradas)> ImportarAsync(
        string json, IArmazenamentoFacial armazenamento, CancellationToken cancellationToken)
    {
        var dados = JsonSerializer.Deserialize<Dictionary<string, List<List<PontoFacial>>>>(json)
                    ?? throw new InvalidDataException("O arquivo não está no formato esperado.");

        var importadas = 0;
        var ignoradas = 0;

        foreach (var (pessoaId, amostras) in dados)
        {
            if (amostras.Count == 0)
                continue;

            if (await armazenamento.ExisteAsync(pessoaId, cancellationToken))
            {
                ignoradas++;
                continue;
            }

            foreach (var amostra in amostras)
                await armazenamento.SalvarAsync(
                    pessoaId, amostra.SelectMany(p => new[] { p.X, p.Y, p.Z }).ToList(), cancellationToken);

            importadas++;
        }

        return (importadas, ignoradas);
    }

    private sealed record PontoFacial(
        [property: System.Text.Json.Serialization.JsonPropertyName("x")] double X,
        [property: System.Text.Json.Serialization.JsonPropertyName("y")] double Y,
        [property: System.Text.Json.Serialization.JsonPropertyName("z")] double Z);
}
