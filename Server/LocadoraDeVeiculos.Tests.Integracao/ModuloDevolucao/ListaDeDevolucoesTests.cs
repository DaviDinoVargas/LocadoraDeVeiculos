using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Handlers;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDevolucao;
using Microsoft.EntityFrameworkCore;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloDevolucao;

/// <summary>
/// Bug achado pelo usuário: depois de finalizar uma devolução, a tabela de devoluções não mostrava o condutor e o cliente
/// ("N/A"). A lista vinha do repositório sem carregar o aluguel (condutor, automóvel, cliente). Cada teste grava os dados num
/// contexto e lê em OUTRO, como numa requisição nova: no mesmo contexto o EF completaria as navegações e esconderia o defeito.
/// </summary>
[TestClass]
[TestCategory("Integração - Lista de devoluções")]
public sealed class ListaDeDevolucoesTests
{
    private sealed class TenantProviderFake(Guid? empresa) : ITenantProvider
    {
        public Guid? EmpresaId => empresa;
        public bool EstaNoCargo(string cargo) => false;
    }

    private readonly Guid _empresaId = Guid.NewGuid();
    private string _banco = null!;

    private LocadoraDeVeiculosDbContext NovoContexto(Guid? empresa = null) =>
        new(new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>().UseInMemoryDatabase(_banco).Options,
            new TenantProviderFake(empresa ?? _empresaId));

    [TestInitialize]
    public void Setup() => _banco = Guid.NewGuid().ToString();

    /// <summary>Cliente, condutor, carro, aluguel e devolução da empresa dada. Devolve (devolução, aluguel).</summary>
    private async Task<(Devolucao Devolucao, Aluguel Aluguel)> GravarDevolucaoAsync(
        Guid empresa, string cliente, string condutor, string placa, string cpf, string cnh)
    {
        await using var contexto = NovoContexto(empresa);

        var grupo = new GrupoAutomovel("Grupo " + placa) { EmpresaId = empresa };
        contexto.Set<GrupoAutomovel>().Add(grupo);

        var carro = new Automovel(placa, "Fiat", "Prata", "Uno", TipoCombustivel.Gasolina, 50, 2020, null, grupo.Id) { EmpresaId = empresa };
        contexto.Set<Automovel>().Add(carro);

        var cli = new ClientePessoaFisica(cliente, "(11) 99999-9999", $"{placa}@teste.com", "Rua Teste, 1",
            cpf, "12.345.678-9", cnh, DateTime.UtcNow.AddYears(2))
        { EmpresaId = empresa };
        contexto.Set<Cliente>().Add(cli);

        var cond = new Condutor(condutor, $"{placa}c@teste.com", cpf, cnh, DateTime.UtcNow.AddYears(1), "(11) 98888-8888", cli.Id)
        { EmpresaId = empresa };
        contexto.Set<Condutor>().Add(cond);

        var aluguel = new Aluguel(cond.Id, carro.Id, cli.Id, DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-1), 300m)
        { EmpresaId = empresa };
        contexto.Set<Aluguel>().Add(aluguel);

        var devolucao = new Devolucao(aluguel.Id, DateTimeOffset.UtcNow, 1200m, 40m, NivelCombustivel.Cheio) { EmpresaId = empresa };
        devolucao.ValorTotal = 350m;
        contexto.Set<Devolucao>().Add(devolucao);

        await contexto.SaveChangesAsync();

        return (devolucao, aluguel);
    }

    [TestMethod]
    public async Task Lista_MostraCondutorClienteEPlacaDeCadaDevolucao_LendoEmOutroContexto()
    {
        await GravarDevolucaoAsync(_empresaId, "Ana Cliente", "Bruno Condutor", "AAA1A11", "111.111.111-11", "11111111111");
        await GravarDevolucaoAsync(_empresaId, "Carla Cliente", "Davi Condutor", "BBB2B22", "222.222.222-22", "22222222222");

        await using var leitura = NovoContexto();
        var resultado = await new SelecionarDevolucoesQueryHandler(new RepositorioDevolucaoEmOrm(leitura))
            .Handle(new SelecionarDevolucoesQuery(), CancellationToken.None);

        Assert.IsTrue(resultado.IsSuccess);
        var linhas = resultado.Value.Registros;
        Assert.AreEqual(2, linhas.Count);

        var ana = linhas.Single(l => l.AutomovelPlaca == "AAA1A11");
        Assert.AreEqual("Bruno Condutor", ana.CondutorNome, "o condutor precisa aparecer na tabela");
        Assert.AreEqual("Ana Cliente", ana.ClienteNome, "o cliente precisa aparecer na tabela");
        Assert.AreEqual(350m, ana.ValorTotal);

        var carla = linhas.Single(l => l.AutomovelPlaca == "BBB2B22");
        Assert.AreEqual("Davi Condutor", carla.CondutorNome);
        Assert.AreEqual("Carla Cliente", carla.ClienteNome);

        Assert.IsFalse(linhas.Any(l => l.CondutorNome == "N/A" || l.ClienteNome == "N/A" || l.AutomovelPlaca == "N/A"),
            "nenhuma linha pode cair em 'N/A' quando o aluguel existe");
    }

    [TestMethod]
    public async Task Lista_DevolucoesDeOutraEmpresaNaoAparecem_MesmoComOsDadosCarregados()
    {
        var outraEmpresa = Guid.NewGuid();
        await GravarDevolucaoAsync(_empresaId, "Ana Cliente", "Bruno Condutor", "AAA1A11", "111.111.111-11", "11111111111");
        await GravarDevolucaoAsync(outraEmpresa, "Segredo Cliente", "Segredo Condutor", "ZZZ9Z99", "999.999.999-99", "99999999999");

        await using var leitura = NovoContexto();
        var resultado = await new SelecionarDevolucoesQueryHandler(new RepositorioDevolucaoEmOrm(leitura))
            .Handle(new SelecionarDevolucoesQuery(), CancellationToken.None);

        var texto = string.Join("|", resultado.Value.Registros.Select(l => $"{l.ClienteNome}{l.CondutorNome}{l.AutomovelPlaca}"));
        Assert.AreEqual(1, resultado.Value.Registros.Count);
        Assert.IsFalse(texto.Contains("Segredo") || texto.Contains("ZZZ9Z99"), "carregar o aluguel não pode furar o isolamento por empresa");
    }

    [TestMethod]
    public async Task DetalheEBuscaPorAluguel_ContinuamMostrandoOsNomes()
    {
        var (devolucao, aluguel) = await GravarDevolucaoAsync(_empresaId, "Ana Cliente", "Bruno Condutor", "AAA1A11", "111.111.111-11", "11111111111");

        await using var leitura = NovoContexto();
        var repositorio = new RepositorioDevolucaoEmOrm(leitura);

        var porId = await repositorio.SelecionarPorIdAsync(devolucao.Id);
        var porAluguel = await repositorio.SelecionarPorAluguelIdAsync(aluguel.Id);

        Assert.AreEqual("Ana Cliente", porId!.Aluguel!.Cliente!.Nome);
        Assert.AreEqual("Bruno Condutor", porId.Aluguel.Condutor!.Nome);
        Assert.AreEqual("AAA1A11", porId.Aluguel.Automovel!.Placa);
        Assert.AreEqual("Ana Cliente", porAluguel!.Aluguel!.Cliente!.Nome);
    }
}
