using LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

namespace LocadoraDeVeiculos.Testes.Unidade.ModuloAutenticacao;

[TestClass]
[TestCategory("Unidade - Limitador de login por conta e IP")]
public sealed class LimitadorDeLoginPorContaTests
{
    private sealed class RelogioFalso : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private readonly RelogioFalso _relogio = new();
    private LimitadorDeLoginPorConta _limitador = null!;

    [TestInitialize]
    public void Setup() => _limitador = new LimitadorDeLoginPorConta(_relogio);

    private void Errar(string email, string? ip, int vezes)
    {
        for (var i = 0; i < vezes; i++) _limitador.RegistrarFalha(email, ip);
    }

    [TestMethod]
    public void BloqueiaDepoisDeCincoErros_NoMesmoPar()
    {
        Errar("a@b.com", "1.1.1.1", 4);
        Assert.IsFalse(_limitador.EstaBloqueado("a@b.com", "1.1.1.1"));

        Errar("a@b.com", "1.1.1.1", 1);
        Assert.IsTrue(_limitador.EstaBloqueado("a@b.com", "1.1.1.1"));
    }

    [TestMethod]
    public void OBloqueioNaoPassaParaOutroIpNemParaOutraConta()
    {
        Errar("a@b.com", "1.1.1.1", 5);

        Assert.IsFalse(_limitador.EstaBloqueado("a@b.com", "2.2.2.2"));
        Assert.IsFalse(_limitador.EstaBloqueado("outra@b.com", "1.1.1.1"));
    }

    [TestMethod]
    public void EmailEhComparadoSemDiferenciarCaixaNemEspacos()
    {
        Errar("A@B.com", "1.1.1.1", 5);

        Assert.IsTrue(_limitador.EstaBloqueado("  a@b.COM ", "1.1.1.1"));
    }

    [TestMethod]
    public void DepoisDaJanelaDe15Minutos_LiberaDeNovo()
    {
        Errar("a@b.com", "1.1.1.1", 5);

        _relogio.Agora = _relogio.Agora.AddMinutes(14);
        Assert.IsTrue(_limitador.EstaBloqueado("a@b.com", "1.1.1.1"));

        _relogio.Agora = _relogio.Agora.AddMinutes(2);
        Assert.IsFalse(_limitador.EstaBloqueado("a@b.com", "1.1.1.1"));
    }

    [TestMethod]
    public void LoginCerto_ZeraOsErros()
    {
        Errar("a@b.com", "1.1.1.1", 4);
        _limitador.Zerar("a@b.com", "1.1.1.1");
        Errar("a@b.com", "1.1.1.1", 4);

        Assert.IsFalse(_limitador.EstaBloqueado("a@b.com", "1.1.1.1"));
    }

    [TestMethod]
    public void SemIpConhecido_NaoBloqueia()
    {
        Errar("a@b.com", "", 20);
        Errar("a@b.com", null, 20);

        Assert.IsFalse(_limitador.EstaBloqueado("a@b.com", null));
        Assert.IsFalse(_limitador.EstaBloqueado("a@b.com", ""));
    }

    [TestMethod]
    public void Ipv6_ValePeloPrefixo64_EnaoPeloEnderecoInteiro()
    {
        for (var i = 1; i <= 5; i++)
            _limitador.RegistrarFalha("a@b.com", $"2001:db8:1:2::{i:x}");

        Assert.IsTrue(_limitador.EstaBloqueado("a@b.com", "2001:db8:1:2:ffff:ffff:ffff:ffff"), "outro endereço do mesmo /64");
        Assert.IsFalse(_limitador.EstaBloqueado("a@b.com", "2001:db8:1:3::1"), "outro /64");
    }

    [TestMethod]
    public void Ipv4MapeadoParaIpv6_EhOMesmoIpv4()
    {
        Errar("a@b.com", "::ffff:203.0.113.9", 5);

        Assert.IsTrue(_limitador.EstaBloqueado("a@b.com", "203.0.113.9"));
    }

    [TestMethod]
    public void OLimiteDoParEhMenorQueODaConta()
    {
        Assert.IsTrue(PoliticaDeBloqueio.TentativasPorContaEIp < PoliticaDeBloqueio.TentativasPorConta,
            "senão um atacante de um IP só trancaria o dono antes de ser barrado");
    }
}
