using LocadoraDeVeiculos.Core.Aplicacao.ModuloPlanoCobranca.Handlers;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Tests.Integracao.Compartilhado;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloPlanoCobranca;

/// <summary>
/// A lista de planos não trazia o grupo (só o detalhe trazia o id), então a tela mostrava uma coluna
/// "Grupo de Veículo" vazia/com GUID. Agora a lista devolve id e nome do grupo.
/// </summary>
[TestClass]
[TestCategory("Integração - Planos com grupo")]
public sealed class ListaDePlanosComGrupoTests
{
    [TestMethod]
    public async Task Lista_TrazIdENomeDoGrupoDeCadaPlano()
    {
        await using var c = await CenarioAluguel.CriarAsync(precoDiaria: 100m);

        // A listagem de planos faz Include(p => p.Empresa): com a FK obrigatória, o provider InMemory
        // descarta planos cuja empresa (Usuario) não existe, então ela precisa estar semeada.
        c.Contexto.Set<Usuario>().Add(new Usuario { Id = c.EmpresaId, FullName = "Locadora Teste", UserName = "locadora@teste.com" });

        var luxo = new GrupoAutomovel("Luxo") { EmpresaId = c.EmpresaId };
        c.Contexto.Set<GrupoAutomovel>().Add(luxo);
        await c.RepositorioPlanoCobranca.CadastrarAsync(
            new PlanoCobranca(luxo.Id, c.EmpresaId, "Plano Luxo", precoDiaria: 400m, precoPorKm: 1m, kmLivreLimite: 50));
        await c.Contexto.SaveChangesAsync();

        var handler = new SelecionarPlanosCobrancaQueryHandler(
            c.RepositorioPlanoCobranca, new RepositorioGrupoAutomovelEmOrm(c.Contexto));

        var resultado = await handler.Handle(new SelecionarPlanosCobrancaQuery(), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        var padrao = resultado.Value.Registros.Single(p => p.Nome == "Plano Padrão");
        var plano = resultado.Value.Registros.Single(p => p.Nome == "Plano Luxo");
        Assert.AreEqual(c.Grupo.Id, padrao.GrupoAutomovelId);
        Assert.AreEqual("Grupo Teste", padrao.GrupoAutomovelNome);
        Assert.AreEqual(luxo.Id, plano.GrupoAutomovelId);
        Assert.AreEqual("Luxo", plano.GrupoAutomovelNome);
    }
}
