using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Validators;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using MediatR;

namespace LocadoraDeVeiculos.Testes.Unidade.Seguranca;

// Corridas de negócio: comandos de aluguel/devolução de uma mesma empresa rodam um de cada vez (reserva dupla, cupom, transições).
[TestClass]
[TestCategory("Unidade - Serialização por empresa")]
public sealed class SerializacaoPorEmpresaTests
{
    private sealed class Tenant(Guid? empresa) : ITenantProvider
    {
        public Guid? EmpresaId => empresa;
        public bool EstaNoCargo(string cargo) => false;
    }

    private sealed record ComandoMarcado : IRequest<int>, ISerializadoPorEmpresa;
    private sealed record ComandoComum : IRequest<int>;
    private sealed record OutroComandoMarcado : IRequest<int>, ISerializadoPorEmpresa;

    private sealed class Medidor
    {
        private int _emAndamento;
        public int MaximoSimultaneo { get; private set; }
        public int Total;

        public async Task<int> ExecutarAsync()
        {
            var agora = Interlocked.Increment(ref _emAndamento);
            lock (this) MaximoSimultaneo = Math.Max(MaximoSimultaneo, agora);
            await Task.Delay(15);
            Interlocked.Decrement(ref _emAndamento);

            return Interlocked.Increment(ref Total);
        }
    }

    private static Task<int> Rodar<TRequest>(TRequest comando, Guid? empresa, Medidor medidor) where TRequest : notnull, IRequest<int> =>
        new SerializarPorEmpresaBehavior<TRequest, int>(new Tenant(empresa))
            .Handle(comando, _ => medidor.ExecutarAsync(), CancellationToken.None);

    [TestMethod]
    public async Task MesmaEmpresa_ComandoMarcado_RodaUmDeCadaVez()
    {
        var empresa = Guid.NewGuid();
        var medidor = new Medidor();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => Rodar(new ComandoMarcado(), empresa, medidor))));

        Assert.AreEqual(1, medidor.MaximoSimultaneo);
        Assert.AreEqual(20, medidor.Total, "todos rodam, só que em fila");
    }

    [TestMethod]
    public async Task EmpresasDiferentes_NaoEsperamUmaPelaOutra()
    {
        var medidor = new Medidor();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Rodar(new ComandoMarcado(), Guid.NewGuid(), medidor))));

        Assert.IsTrue(medidor.MaximoSimultaneo > 1, "empresas diferentes seguem em paralelo");
    }

    [TestMethod]
    public async Task ComandosDeTiposDiferentes_DaMesmaEmpresa_TambemRodamUmDeCadaVez()
    {
        // "iniciar" e "cancelar" são comandos diferentes: se cada tipo tivesse a sua própria fila, os dois passariam juntos pela checagem
        var empresa = Guid.NewGuid();
        var medidor = new Medidor();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() => i % 2 == 0
            ? Rodar(new ComandoMarcado(), empresa, medidor)
            : Rodar(new OutroComandoMarcado(), empresa, medidor))));

        Assert.AreEqual(1, medidor.MaximoSimultaneo, "a fila é da empresa, não do tipo do comando");
        Assert.AreEqual(20, medidor.Total);
    }

    [TestMethod]
    public async Task ComandoSemMarcador_NaoEhSerializado()
    {
        var empresa = Guid.NewGuid();
        var medidor = new Medidor();

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() => Rodar(new ComandoComum(), empresa, medidor))));

        Assert.IsTrue(medidor.MaximoSimultaneo > 1, "consultas e outros comandos não esperam na fila");
    }

    [TestMethod]
    public async Task SemEmpresaNoContexto_NaoTrava()
    {
        var medidor = new Medidor();

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => Rodar(new ComandoMarcado(), null, medidor))));

        Assert.AreEqual(5, medidor.Total);
    }

    [TestMethod]
    public async Task ErroNoComando_LiberaAFilaParaOsProximos()
    {
        var empresa = Guid.NewGuid();
        var comportamento = new SerializarPorEmpresaBehavior<ComandoMarcado, int>(new Tenant(empresa));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            comportamento.Handle(new ComandoMarcado(), _ => throw new InvalidOperationException("falhou"), CancellationToken.None));

        var depois = await comportamento.Handle(new ComandoMarcado(), _ => Task.FromResult(7), CancellationToken.None);

        Assert.AreEqual(7, depois, "a trava foi solta mesmo com a exceção");
    }

    [TestMethod]
    public void ComandosDeAluguelEDevolucao_TemOMarcador()
    {
        foreach (var tipo in new[]
                 {
                     typeof(CadastrarAluguelCommand), typeof(EditarAluguelCommand), typeof(IniciarAluguelCommand),
                     typeof(CancelarAluguelCommand), typeof(ExcluirAluguelCommand),
                     typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands.RegistrarDevolucaoCommand),
                     typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao.Commands.ExcluirDevolucaoCommand)
                 })
            Assert.IsTrue(typeof(ISerializadoPorEmpresa).IsAssignableFrom(tipo), $"{tipo.Name} precisa do marcador");
    }

    [TestMethod]
    public void Validador_AluguelDe365DiasPassa_DeMaisDeUmAnoNao()
    {
        var saida = DateTimeOffset.UtcNow.AddDays(2);
        var validador = new CadastrarAluguelCommandValidator();
        CadastrarAluguelCommand Com(int dias) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), saida, saida.AddDays(dias), 0, []);

        Assert.IsTrue(validador.Validate(Com(365)).IsValid);
        var longo = validador.Validate(Com(366));
        Assert.IsFalse(longo.IsValid);
        StringAssert.Contains(longo.Errors.Single().ErrorMessage, "365");
    }
}
