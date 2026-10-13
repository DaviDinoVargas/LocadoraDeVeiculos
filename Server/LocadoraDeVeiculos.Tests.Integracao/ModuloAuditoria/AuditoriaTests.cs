using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAuditoria;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAuditoria;

// S14: quem fez o quê, quando e de onde, isolado por empresa e sem guardar valores.
[TestClass]
[TestCategory("Integração - Auditoria")]
public sealed class AuditoriaTests
{
    private sealed class Tenant : ITenantProvider
    {
        public Guid? EmpresaId { get; init; }
        public Guid? UsuarioId { get; init; }
        public string? EnderecoIp { get; init; }
        public bool EstaNoCargo(string cargo) => false;
    }

    private static LocadoraDeVeiculosDbContext Contexto(string banco, Guid? empresa, Guid? usuario = null, string? ip = null) =>
        new(new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>().UseInMemoryDatabase(banco).Options,
            new Tenant { EmpresaId = empresa, UsuarioId = usuario, EnderecoIp = ip });

    private static GrupoAutomovel NovoGrupo(Guid empresa, string nome = "Econômico") =>
        new(nome) { EmpresaId = empresa };

    [TestMethod]
    public async Task CriarEntidade_GravaRegistroComQuemQuandoEOrigem()
    {
        var banco = Guid.NewGuid().ToString();
        var empresa = Guid.NewGuid();
        var usuario = Guid.NewGuid();
        var grupo = NovoGrupo(empresa);

        await using (var contexto = Contexto(banco, empresa, usuario, "203.0.113.7"))
        {
            contexto.Set<GrupoAutomovel>().Add(grupo);
            await contexto.SaveChangesAsync();
        }

        await using var leitura = Contexto(banco, empresa);
        var registro = await new RepositorioAuditoriaEmOrm(leitura).ListarRecentesAsync(10, CancellationToken.None);

        Assert.AreEqual(1, registro.Count);
        Assert.AreEqual("Criado", registro[0].Acao);
        Assert.AreEqual(nameof(GrupoAutomovel), registro[0].Entidade);
        Assert.AreEqual(grupo.Id, registro[0].EntidadeId);
        Assert.AreEqual(usuario, registro[0].UsuarioId);
        Assert.AreEqual("203.0.113.7", registro[0].EnderecoIp);
        Assert.AreEqual(empresa, registro[0].EmpresaId);
        Assert.IsTrue((DateTime.UtcNow - registro[0].EmUtc).TotalMinutes < 1);
    }

    [TestMethod]
    public async Task AlterarEntidade_GravaSoOsNomesDosCamposNuncaOsValores()
    {
        var banco = Guid.NewGuid().ToString();
        var empresa = Guid.NewGuid();
        var grupo = NovoGrupo(empresa, "Nome Sigiloso Antigo");

        await using (var contexto = Contexto(banco, empresa))
        {
            contexto.Set<GrupoAutomovel>().Add(grupo);
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = Contexto(banco, empresa))
        {
            var existente = await contexto.Set<GrupoAutomovel>().SingleAsync();
            existente.Nome = "Nome Sigiloso Novo";
            await contexto.SaveChangesAsync();
        }

        await using var leitura = Contexto(banco, empresa);
        var registros = await new RepositorioAuditoriaEmOrm(leitura).ListarRecentesAsync(10, CancellationToken.None);
        var alteracao = registros.Single(r => r.Acao == "Alterado");

        Assert.AreEqual("Nome", alteracao.CamposAlterados);
        Assert.IsFalse(registros.Any(r => (r.CamposAlterados ?? "").Contains("Sigiloso")), "valores não podem ir para a trilha");
    }

    [TestMethod]
    public async Task ExclusaoLogica_VirraAcaoExcluido()
    {
        var banco = Guid.NewGuid().ToString();
        var empresa = Guid.NewGuid();

        await using (var contexto = Contexto(banco, empresa))
        {
            contexto.Set<GrupoAutomovel>().Add(NovoGrupo(empresa));
            await contexto.SaveChangesAsync();
        }

        await using (var contexto = Contexto(banco, empresa))
        {
            (await contexto.Set<GrupoAutomovel>().SingleAsync()).Excluir();
            await contexto.SaveChangesAsync();
        }

        await using var leitura = Contexto(banco, empresa);
        var acoes = (await new RepositorioAuditoriaEmOrm(leitura).ListarRecentesAsync(10, CancellationToken.None))
            .Select(r => r.Acao).ToList();

        CollectionAssert.AreEquivalent(new[] { "Criado", "Excluído" }, acoes);
    }

    [TestMethod]
    public async Task EmpresaB_NaoEnxergaAAuditoriaDaEmpresaA()
    {
        var banco = Guid.NewGuid().ToString();
        var empresaA = Guid.NewGuid();
        var empresaB = Guid.NewGuid();

        await using (var contexto = Contexto(banco, empresaA))
        {
            contexto.Set<GrupoAutomovel>().Add(NovoGrupo(empresaA));
            await contexto.SaveChangesAsync();
        }

        await using var comoB = Contexto(banco, empresaB);
        await using var semTenant = Contexto(banco, null);
        await using var comoA = Contexto(banco, empresaA);

        Assert.AreEqual(0, (await new RepositorioAuditoriaEmOrm(comoB).ListarRecentesAsync(10, CancellationToken.None)).Count);
        Assert.AreEqual(0, (await new RepositorioAuditoriaEmOrm(semTenant).ListarRecentesAsync(10, CancellationToken.None)).Count);
        Assert.AreEqual(1, (await new RepositorioAuditoriaEmOrm(comoA).ListarRecentesAsync(10, CancellationToken.None)).Count);
    }

    [TestMethod]
    public async Task Rosto_CadastroERemocaoSaoAuditadosSemGuardarOPessoaId()
    {
        var banco = Guid.NewGuid().ToString();
        var empresa = Guid.NewGuid();
        var cifrador = new LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao.CifradorFacial(
            new System.Collections.Generic.Dictionary<byte, byte[]> { [1] = new byte[32] });

        await using (var contexto = Contexto(banco, empresa, Guid.NewGuid()))
        {
            var armazenamento = new LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao.ArmazenamentoFacialEmOrm(contexto, cifrador);
            await armazenamento.SalvarAsync("usuario:ana@loja.com", [0.1, 0.2], CancellationToken.None);
            await armazenamento.RemoverAsync("usuario:ana@loja.com", CancellationToken.None);
        }

        await using var leitura = Contexto(banco, empresa);
        var registros = await new RepositorioAuditoriaEmOrm(leitura).ListarRecentesAsync(10, CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { "Criado", "Removido" }, registros.Select(r => r.Acao).ToList());
        Assert.IsTrue(registros.All(r => r.Entidade == nameof(AmostraFacial)));
        Assert.IsFalse(registros.Any(r => (r.CamposAlterados ?? "").Contains("ana")));
    }

    [TestMethod]
    public async Task LimiteDaConsulta_Respeitado_EOrdemEDaMaisRecenteParaAMaisAntiga()
    {
        var banco = Guid.NewGuid().ToString();
        var empresa = Guid.NewGuid();

        await using (var contexto = Contexto(banco, empresa))
        {
            for (var i = 0; i < 5; i++)
            {
                contexto.Set<GrupoAutomovel>().Add(NovoGrupo(empresa, $"Grupo {i}"));
                await contexto.SaveChangesAsync();
                await Task.Delay(5);
            }
        }

        await using var leitura = Contexto(banco, empresa);
        var registros = await new RepositorioAuditoriaEmOrm(leitura).ListarRecentesAsync(3, CancellationToken.None);

        Assert.AreEqual(3, registros.Count);
        Assert.IsTrue(registros[0].EmUtc >= registros[1].EmUtc && registros[1].EmUtc >= registros[2].EmUtc);
    }
}
