using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using Microsoft.EntityFrameworkCore;

namespace LocadoraDeVeiculos.Testes.Unidade.Seguranca;

// Corrida de cadastro: duas requisições iguais passam juntas pela checagem "já existe?" e o índice único do banco barra a segunda.
// Para quem chamou isso é "já existe" (409), nunca "erro interno" (500) — e o texto do SQL não pode ir para a resposta.
[TestClass]
[TestCategory("Unidade - Violação de unicidade")]
public sealed class ViolacaoDeUnicidadeTests
{
    /// <summary>Imita o SqlException do driver (que não tem construtor público): o reconhecimento é pelo nome do tipo e pelo Number.</summary>
    private sealed class SqlException(int numero, string mensagem) : Exception(mensagem)
    {
        public int Number { get; } = numero;
    }

    private static DbUpdateException ComErroDoBanco(int numero) =>
        new("Falha ao gravar", new SqlException(numero, "Cannot insert duplicate key row in object 'dbo.Clientes' with unique index 'IX_Clientes_Cpf'."));

    [TestMethod]
    [DataRow(2601)]   // índice único
    [DataRow(2627)]   // restrição UNIQUE / chave primária
    public void ErrosDeIndiceUnico_SaoReconhecidos_MesmoEmbrulhadosNoErroDoEfCore(int numero)
    {
        Assert.IsTrue(ViolacaoDeUnicidade.Eh(ComErroDoBanco(numero)));
        Assert.IsTrue(ViolacaoDeUnicidade.Eh(new InvalidOperationException("externo", ComErroDoBanco(numero))), "vale em qualquer nível da cadeia");
    }

    [TestMethod]
    [DataRow(547)]    // violação de chave estrangeira: é outro problema, continua sendo erro interno
    [DataRow(1205)]   // deadlock
    [DataRow(0)]
    public void OutrosErrosDoBanco_NaoSaoTratadosComoDuplicidade(int numero)
    {
        Assert.IsFalse(ViolacaoDeUnicidade.Eh(ComErroDoBanco(numero)));
    }

    [TestMethod]
    public void ExcecoesComunsENulo_NaoSaoDuplicidade()
    {
        Assert.IsFalse(ViolacaoDeUnicidade.Eh(null));
        Assert.IsFalse(ViolacaoDeUnicidade.Eh(new InvalidOperationException("2627")), "só o número do erro do SQL Server conta, não texto solto");
        Assert.IsFalse(ViolacaoDeUnicidade.Eh(new DbUpdateException("sem causa")));
    }

    [TestMethod]
    public void ErroDeDuplicidadeVira409ComMensagemFixa_SemVazarOTextoDoSql()
    {
        var erro = ResultadosErro.ExcecaoInternaErro(ComErroDoBanco(2627));

        Assert.AreEqual("RegistroDuplicado", erro.Metadata["TipoErro"]);
        var texto = string.Join(" ", erro.Reasons.Select(r => r.Message)) + erro.Message;
        StringAssert.Contains(texto, "Já existe um registro");
        Assert.IsFalse(texto.Contains("IX_Clientes_Cpf") || texto.Contains("dbo."), "nome de índice e de tabela não saem na resposta");
    }

    [TestMethod]
    public void QualquerOutraExcecaoContinuaSendoErroInterno_SemDetalhes()
    {
        var erro = ResultadosErro.ExcecaoInternaErro(new InvalidOperationException("senha=abc na cadeia de conexão"));

        Assert.AreEqual("ExcecaoInterna", erro.Metadata["TipoErro"]);
        Assert.IsFalse((erro.Message + string.Join(" ", erro.Reasons.Select(r => r.Message))).Contains("senha=abc"));
    }
}
