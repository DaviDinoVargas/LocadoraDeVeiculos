using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDesafioCupom;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloDesafioCupom;

[TestClass]
[TestCategory("Integração - Desafio de Cupom")]
public sealed class RepositorioDesafioCupomOrmTests
{
    private sealed class TenantProviderFake : ITenantProvider
    {
        public Guid? EmpresaId { get; init; }
        public bool EstaNoCargo(string cargo) => false;
    }

    private static LocadoraDeVeiculosDbContext CriarContexto(string nomeBanco, Guid empresaId)
    {
        var options = new DbContextOptionsBuilder<LocadoraDeVeiculosDbContext>()
            .UseInMemoryDatabase(nomeBanco)
            .Options;

        return new LocadoraDeVeiculosDbContext(options, new TenantProviderFake { EmpresaId = empresaId });
    }

    private static Aluguel CriarAluguelConcluido(Guid clienteId, Guid empresaId, DateTimeOffset dataSaida)
    {
        var aluguel = new Aluguel(
            condutorId: Guid.NewGuid(),
            automovelId: Guid.NewGuid(),
            clienteId: clienteId,
            dataSaida: dataSaida,
            dataRetornoPrevisto: dataSaida.AddDays(3),
            valorPrevisto: 300m)
        {
            EmpresaId = empresaId,
            Status = StatusAluguel.Concluido
        };
        return aluguel;
    }

    [TestMethod]
    public async Task ContarAlugueisConcluidosDoClienteAsync_ContaSoDentroDoPeriodoEComStatusConcluido()
    {
        var empresaId = Guid.NewGuid();
        var clienteId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorio = new RepositorioDesafioCupomEmOrm(contexto);

        // Dentro do período de 30 dias, concluído -> conta
        contexto.Set<Aluguel>().Add(CriarAluguelConcluido(clienteId, empresaId, DateTimeOffset.UtcNow.AddDays(-5)));
        // Dentro do período, mas ainda em andamento -> não conta
        var emAndamento = CriarAluguelConcluido(clienteId, empresaId, DateTimeOffset.UtcNow.AddDays(-2));
        emAndamento.Status = StatusAluguel.EmAndamento;
        contexto.Set<Aluguel>().Add(emAndamento);
        // Fora do período (há 60 dias, janela é de 30) -> não conta
        contexto.Set<Aluguel>().Add(CriarAluguelConcluido(clienteId, empresaId, DateTimeOffset.UtcNow.AddDays(-60)));
        // De outro cliente -> não conta
        contexto.Set<Aluguel>().Add(CriarAluguelConcluido(Guid.NewGuid(), empresaId, DateTimeOffset.UtcNow.AddDays(-1)));

        await contexto.SaveChangesAsync();

        var total = await repositorio.ContarAlugueisConcluidosDoClienteAsync(clienteId, periodoDias: 30);

        Assert.AreEqual(1, total);
    }

    [TestMethod]
    public async Task SelecionarTodosAsync_IncluiOCupomRecompensaVinculado()
    {
        var empresaId = Guid.NewGuid();
        await using var contexto = CriarContexto(Guid.NewGuid().ToString(), empresaId);
        var repositorio = new RepositorioDesafioCupomEmOrm(contexto);

        var cupom = new Cupom("RECOMPENSA5", "Cupom de recompensa", TipoDesconto.Percentual, 5m,
            DateTimeOffset.UtcNow.AddDays(90), limiteUsos: null, parceiroId: null)
        {
            EmpresaId = empresaId
        };
        contexto.Set<Cupom>().Add(cupom);
        await contexto.SaveChangesAsync();

        var desafio = new DesafioCupom("Cliente fiel", "5 aluguéis em 60 dias", metaQuantidadeAlugueis: 5, periodoDias: 60, cupomRecompensaId: cupom.Id)
        {
            EmpresaId = empresaId
        };
        await repositorio.CadastrarAsync(desafio);
        await contexto.SaveChangesAsync();

        var registros = await repositorio.SelecionarTodosAsync();

        Assert.AreEqual(1, registros.Count);
        Assert.IsNotNull(registros[0].CupomRecompensa);
        Assert.AreEqual("RECOMPENSA5", registros[0].CupomRecompensa!.Codigo);
    }

    [TestMethod]
    public async Task EmpresaB_NaoDeveEnxergarDesafioDaEmpresaA()
    {
        var nomeBanco = Guid.NewGuid().ToString();
        var empresaA = Guid.NewGuid();
        var empresaB = Guid.NewGuid();

        await using (var contextoA = CriarContexto(nomeBanco, empresaA))
        {
            var cupom = new Cupom("SOEMPRESAA", "Cupom", TipoDesconto.Percentual, 10m,
                DateTimeOffset.UtcNow.AddDays(30), limiteUsos: null, parceiroId: null)
            {
                EmpresaId = empresaA
            };
            contextoA.Set<Cupom>().Add(cupom);
            await contextoA.SaveChangesAsync();

            var desafio = new DesafioCupom("Desafio da empresa A", "Descrição", 3, 30, cupom.Id) { EmpresaId = empresaA };
            contextoA.Set<DesafioCupom>().Add(desafio);
            await contextoA.SaveChangesAsync();
        }

        await using var contextoB = CriarContexto(nomeBanco, empresaB);
        var repositorioB = new RepositorioDesafioCupomEmOrm(contextoB);

        var registros = await repositorioB.SelecionarTodosAsync();

        Assert.AreEqual(0, registros.Count);
    }
}
