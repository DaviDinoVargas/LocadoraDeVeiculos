using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;
using LocadoraDeVeiculos.WebApi.Config.Orm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S1: migração única do antigo faces.json para o banco cifrado.
[TestClass]
[TestCategory("Web API - Importação facial")]
public sealed class ImportadorDeFacesTests
{
    private const string Json = """
        {
          "usuario:ana@loja.com": [
            [ {"x": 0.1, "y": 0.2, "z": 0.3}, {"x": 0.4, "y": 0.5, "z": 0.6} ],
            [ {"x": 0.7, "y": 0.8, "z": 0.9}, {"x": 1.0, "y": 1.1, "z": 1.2} ]
          ],
          "usuario:bia@loja.com": [],
          "usuario:caio@loja.com": [
            [ {"x": 2.0, "y": 2.1, "z": 2.2} ]
          ]
        }
        """;

    private static (LocadoraDeVeiculosDbContext Contexto, ArmazenamentoFacialEmOrm Armazenamento) Novo()
    {
        var contexto = new LocadoraDeVeiculosDbContext(
            new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var cifrador = new CifradorFacial(new Dictionary<byte, byte[]> { [1] = new byte[32] });

        return (contexto, new ArmazenamentoFacialEmOrm(contexto, cifrador));
    }

    [TestMethod]
    public async Task Importa_AchatandoCadaAmostraEmXyz()
    {
        var (contexto, armazenamento) = Novo();
        await using var _ = contexto;

        var (importadas, ignoradas) = await ImportadorDeFaces.ImportarAsync(Json, armazenamento, CancellationToken.None);

        Assert.AreEqual(2, importadas, "quem não tem amostras (bia) não conta");
        Assert.AreEqual(0, ignoradas);

        var ana = await armazenamento.ObterAsync("usuario:ana@loja.com", CancellationToken.None);
        Assert.AreEqual(2, ana.Count);
        CollectionAssert.AreEqual(new[] { 0.1, 0.2, 0.3, 0.4, 0.5, 0.6 }, ana[0].ToArray());
        CollectionAssert.AreEqual(new[] { 0.7, 0.8, 0.9, 1.0, 1.1, 1.2 }, ana[1].ToArray());
        Assert.IsFalse(await armazenamento.ExisteAsync("usuario:bia@loja.com", CancellationToken.None));
    }

    [TestMethod]
    public async Task Importa_EhIdempotente_NaoDuplicaQuemJaEstaNoBanco()
    {
        var (contexto, armazenamento) = Novo();
        await using var _ = contexto;

        await ImportadorDeFaces.ImportarAsync(Json, armazenamento, CancellationToken.None);
        var (importadas, ignoradas) = await ImportadorDeFaces.ImportarAsync(Json, armazenamento, CancellationToken.None);

        Assert.AreEqual(0, importadas);
        Assert.AreEqual(2, ignoradas);
        Assert.AreEqual(2, (await armazenamento.ObterAsync("usuario:ana@loja.com", CancellationToken.None)).Count);
    }

    [TestMethod]
    public async Task Importa_ArquivoForaDoFormato_Falha()
    {
        var (contexto, armazenamento) = Novo();
        await using var _ = contexto;

        await Assert.ThrowsExceptionAsync<System.Text.Json.JsonException>(() =>
            ImportadorDeFaces.ImportarAsync("[1,2,3]", armazenamento, CancellationToken.None));
    }

    [TestMethod]
    public async Task Executar_ApagaOArquivoSoQuandoPedido()
    {
        var pasta = Path.Combine(Path.GetTempPath(), "faces-" + Guid.NewGuid());
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, "faces.json");
        File.WriteAllText(arquivo, Json);

        try
        {
            var (contexto, armazenamento) = Novo();
            await using var _ = contexto;
            var servicos = new ServicosFalsos(armazenamento);
            var saida = new StringWriter();

            var codigo = await ImportadorDeFaces.ExecutarAsync(servicos, ["--importar-faces", arquivo], saida);
            Assert.AreEqual(0, codigo);
            Assert.IsTrue(File.Exists(arquivo), "sem --apagar-origem o arquivo fica");
            StringAssert.Contains(saida.ToString(), "NÃO foi apagado");

            codigo = await ImportadorDeFaces.ExecutarAsync(servicos, ["--importar-faces", arquivo, "--apagar-origem"], new StringWriter());
            Assert.AreEqual(0, codigo);
            Assert.IsFalse(File.Exists(arquivo));
        }
        finally
        {
            Directory.Delete(pasta, recursive: true);
        }
    }

    [TestMethod]
    public async Task Executar_SemCaminhoOuComArquivoInexistente_RetornaCodigoDeErro()
    {
        var (contexto, armazenamento) = Novo();
        await using var _ = contexto;
        var servicos = new ServicosFalsos(armazenamento);

        Assert.AreEqual(2, await ImportadorDeFaces.ExecutarAsync(servicos, ["--importar-faces"], new StringWriter()));
        Assert.AreEqual(2, await ImportadorDeFaces.ExecutarAsync(servicos, ["--importar-faces", "nao-existe.json"], new StringWriter()));
    }

    [TestMethod]
    public void Argumentos_SaoReconhecidos()
    {
        Assert.IsTrue(ImportadorDeFaces.FoiSolicitado(["--importar-faces", "x.json"]));
        Assert.IsFalse(ImportadorDeFaces.FoiSolicitado(["--urls", "http://x"]));
        Assert.AreEqual("x.json", ImportadorDeFaces.CaminhoDoArquivo(["--importar-faces", "x.json", "--apagar-origem"]));
        Assert.IsNull(ImportadorDeFaces.CaminhoDoArquivo(["--importar-faces", "--apagar-origem"]));
    }

    private sealed class ServicosFalsos(IArmazenamentoFacial armazenamento) : IServiceProvider, IServiceScopeFactory, IServiceScope
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceScopeFactory)) return this;
            if (serviceType == typeof(IArmazenamentoFacial)) return armazenamento;
            return null;
        }

        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public void Dispose() { }
    }
}
