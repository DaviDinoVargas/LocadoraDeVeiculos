using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Commands;
using MediatR;

namespace LocadoraDeVeiculos.Testes.Unidade.Seguranca;

/// <summary>
/// Estudo com <c>System.Reflection</c>: o teste manual (<see cref="SerializacaoPorEmpresaTests"/>) confere uma lista de comandos escrita à mão, então
/// um comando NOVO de aluguel ou devolução esquecido fora da lista passaria despercebido. Aqui o teste varre o assembly inteiro atrás de todo tipo que
/// implementa <c>IRequest&lt;&gt;</c> nesses módulos e exige: ou o marcador <see cref="ISerializadoPorEmpresa"/> (muda estado, roda um por vez por empresa),
/// ou um lugar na lista explícita de consultas (só lê). Esquecer de decidir reprova.
/// </summary>
[TestClass]
[TestCategory("Reflexão")]
public sealed class ComandosSerializadosPorReflexaoTests
{
    private static readonly string[] Modulos =
    [
        "LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel",
        "LocadoraDeVeiculos.Core.Aplicacao.ModuloDevolucao"
    ];

    /// <summary>
    /// Consultas: só leem, não precisam entrar na fila. Lista explícita (e não "nome começa com Selecionar") de propósito: um comando novo
    /// que por acaso se chame Selecionar... não ganharia a isenção sozinho, alguém precisa escrevê-lo aqui.
    /// </summary>
    private static readonly string[] Consultas =
    [
        "SelecionarAlugueisQuery", "SelecionarAluguelPorIdQuery", "SimularAluguelQuery",
        "SelecionarAlugueisPorStatusQuery", "SelecionarAlugueisEmAbertoQuery",
        "SelecionarDevolucoesQuery", "SelecionarDevolucaoPorIdQuery", "SelecionarDevolucaoPorAluguelIdQuery"
    ];

    private static bool EhRequisicaoDoMediatR(Type tipo) =>
        tipo is { IsClass: true, IsAbstract: false }
        && tipo.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

    /// <summary>Requisições que nem têm o marcador nem estão na lista de consultas: as que o time esqueceu de decidir.</summary>
    private static List<string> SemDecisao(IEnumerable<Type> tipos, IEnumerable<string> consultas)
    {
        var leitura = consultas.ToHashSet();
        return tipos
            .Where(EhRequisicaoDoMediatR)
            .Where(t => !typeof(ISerializadoPorEmpresa).IsAssignableFrom(t) && !leitura.Contains(t.Name))
            .Select(t => t.FullName ?? t.Name)
            .Order()
            .ToList();
    }

    private static List<Type> TiposDosModulos() =>
        typeof(CadastrarAluguelCommand).Assembly.GetTypes()
            .Where(t => t.Namespace is not null && Modulos.Any(m => t.Namespace.StartsWith(m, StringComparison.Ordinal)))
            .ToList();

    [TestMethod]
    public void TodoComandoDeAluguelEDevolucao_OuTemOMarcador_OuEstaDeclaradoComoConsulta()
    {
        var semDecisao = SemDecisao(TiposDosModulos(), Consultas);

        Assert.AreEqual(0, semDecisao.Count,
            "Requisição de aluguel/devolução sem ISerializadoPorEmpresa e fora da lista de consultas: " + string.Join("; ", semDecisao));
    }

    [TestMethod]
    public void AListaDeConsultas_NaoTemNomeQueSumiu_NemConsultaQueRecebeuOMarcador()
    {
        var porNome = TiposDosModulos().Where(EhRequisicaoDoMediatR).ToDictionary(t => t.Name);

        foreach (var nome in Consultas)
        {
            Assert.IsTrue(porNome.ContainsKey(nome), $"{nome} saiu do código: tire da lista de consultas");
            Assert.IsFalse(typeof(ISerializadoPorEmpresa).IsAssignableFrom(porNome[nome]), $"{nome} é consulta mas está marcada: entra na fila sem precisar");
        }
    }

    [TestMethod]
    public void OsSeteComandosQueMudamEstado_SaoOsMarcados_ENadaMais()
    {
        var marcados = TiposDosModulos().Where(EhRequisicaoDoMediatR).Where(t => typeof(ISerializadoPorEmpresa).IsAssignableFrom(t))
            .Select(t => t.Name).Order().ToList();

        CollectionAssert.AreEqual(new[]
        {
            "CadastrarAluguelCommand", "CancelarAluguelCommand", "EditarAluguelCommand", "ExcluirAluguelCommand",
            "ExcluirDevolucaoCommand", "IniciarAluguelCommand", "RegistrarDevolucaoCommand"
        }, marcados);
    }

    [TestMethod]
    public void OMarcador_SoEUsadoEmRequisicoesDoMediatR_PoisSoElasPassamPeloBehavior()
    {
        var foraDoPipeline = typeof(ISerializadoPorEmpresa).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISerializadoPorEmpresa).IsAssignableFrom(t) && !EhRequisicaoDoMediatR(t))
            .Select(t => t.Name)
            .ToList();

        Assert.AreEqual(0, foraDoPipeline.Count, "marcador em tipo que nunca passa pelo behavior: " + string.Join("; ", foraDoPipeline));
    }

    [TestMethod]
    public void ExclusoesQueDisputamComAlugueis_ECadastroDeFuncionario_TambemEntramNaFila()
    {
        // Achado pelo ataque J15/J10: excluir um cliente/condutor/carro/taxa enquanto um aluguel dele é criado deixava aluguel órfão, e dois
        // cadastros de funcionário com o mesmo CPF passavam juntos pela checagem. Estes comandos competem com os de aluguel pela mesma fila.
        var fora = new[]
            {
                typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloCliente.Commands.ExcluirClienteCommand),
                typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloCondutor.Commands.ExcluirCondutorCommand),
                typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloAutomovel.Commands.ExcluirAutomovelCommand),
                typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloTaxaServico.Commands.ExcluirTaxaServicoCommand),
                typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands.CadastrarFuncionarioCommand),
                typeof(LocadoraDeVeiculos.Core.Aplicacao.ModuloFuncionario.Commands.EditarFuncionarioCommand)
            }
            .Where(tipo => !typeof(ISerializadoPorEmpresa).IsAssignableFrom(tipo))
            .Select(tipo => tipo.Name)
            .ToList();

        Assert.AreEqual(0, fora.Count, "sem o marcador: " + string.Join("; ", fora));
    }

    // -------- A varredura realmente pega o esquecimento: tipos de propósito, no mesmo formato dos reais --------

    private sealed record NovoComandoEsquecido(Guid Id) : IRequest<int>;
    private sealed record NovoComandoMarcado(Guid Id) : IRequest<int>, ISerializadoPorEmpresa;
    private sealed record ConsultaDeclarada : IRequest<int>;
    private sealed record NaoEhRequisicao(Guid Id);
    private abstract record BaseAbstrata : IRequest<int>;

    [TestMethod]
    public void Varredura_PegaOComandoEsquecido_EAceitaOMarcadoOUmaConsultaDeclarada()
    {
        var tipos = new[] { typeof(NovoComandoEsquecido), typeof(NovoComandoMarcado), typeof(ConsultaDeclarada), typeof(NaoEhRequisicao), typeof(BaseAbstrata) };

        var semDecisao = SemDecisao(tipos, [nameof(ConsultaDeclarada)]);

        Assert.AreEqual(1, semDecisao.Count);
        StringAssert.Contains(semDecisao[0], nameof(NovoComandoEsquecido));
    }

    [TestMethod]
    public void Varredura_NaoConfundeNomeParecidoComConsulta_SoOQueEstaNaListaEIsentado()
    {
        // um comando que "parece" consulta pelo nome, mas não foi declarado, continua exigindo decisão
        var semDecisao = SemDecisao([typeof(SelecionarEExcluirTudoCommand)], Consultas);

        Assert.AreEqual(1, semDecisao.Count);
    }

    private sealed record SelecionarEExcluirTudoCommand : IRequest<int>;
}
