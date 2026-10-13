using LocadoraDeVeiculos.Tests.WebApi.Compartilhado;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace LocadoraDeVeiculos.Tests.WebApi;

// S18: o e-mail de "esqueci minha senha" sai de verdade pelo SMTP e o link recebido redefine a senha.
// Um servidor SMTP mínimo, em memória, faz o papel do provedor de e-mail.
[TestClass]
[TestCategory("Web API - Recuperação de senha por SMTP")]
public sealed class RecuperacaoDeSenhaPorSmtpTests
{
    private const string Senha = "Senha@12345";
    private const string NovaSenha = "NovaSenha@678";

    private sealed record EmailRecebido(string De, string Para, string Dados);

    /// <summary>SMTP de mentira: aceita qualquer remetente, guarda cada mensagem recebida.</summary>
    private sealed class ServidorSmtpDeTeste : IDisposable
    {
        private readonly TcpListener _escuta = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _parada = new();
        public List<EmailRecebido> Recebidos { get; } = [];
        public int Porta => ((IPEndPoint)_escuta.LocalEndpoint).Port;

        public ServidorSmtpDeTeste()
        {
            _escuta.Start();
            _ = Task.Run(AceitarAsync);
        }

        private async Task AceitarAsync()
        {
            try
            {
                while (!_parada.IsCancellationRequested)
                {
                    var cliente = await _escuta.AcceptTcpClientAsync(_parada.Token);
                    _ = Task.Run(() => AtenderAsync(cliente));
                }
            }
            catch (Exception) when (_parada.IsCancellationRequested) { }
        }

        private async Task AtenderAsync(TcpClient cliente)
        {
            using var _ = cliente;
            await using var rede = cliente.GetStream();
            using var leitor = new StreamReader(rede, Encoding.ASCII);
            await using var escritor = new StreamWriter(rede, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            string de = "", para = "";

            await escritor.WriteLineAsync("220 smtp.teste ESMTP");

            while (await leitor.ReadLineAsync() is { } linha)
            {
                var comando = linha.ToUpperInvariant();

                if (comando.StartsWith("EHLO") || comando.StartsWith("HELO"))
                    await escritor.WriteLineAsync("250 smtp.teste");
                else if (comando.StartsWith("MAIL FROM"))
                {
                    de = linha[(linha.IndexOf('<') + 1)..linha.IndexOf('>')];
                    await escritor.WriteLineAsync("250 ok");
                }
                else if (comando.StartsWith("RCPT TO"))
                {
                    para = linha[(linha.IndexOf('<') + 1)..linha.IndexOf('>')];
                    await escritor.WriteLineAsync("250 ok");
                }
                else if (comando == "DATA")
                {
                    await escritor.WriteLineAsync("354 envie");
                    var dados = new StringBuilder();
                    while (await leitor.ReadLineAsync() is { } l && l != ".")
                        dados.AppendLine(l);
                    lock (Recebidos) Recebidos.Add(new EmailRecebido(de, para, dados.ToString()));
                    await escritor.WriteLineAsync("250 aceito");
                }
                else if (comando == "QUIT")
                {
                    await escritor.WriteLineAsync("221 tchau");
                    return;
                }
                else
                    await escritor.WriteLineAsync("250 ok");
            }
        }

        public async Task<EmailRecebido?> EsperarAsync(TimeSpan prazo)
        {
            var fim = DateTime.UtcNow + prazo;
            while (DateTime.UtcNow < fim)
            {
                lock (Recebidos)
                    if (Recebidos.Count > 0)
                        return Recebidos[0];
                await Task.Delay(100);
            }
            return null;
        }

        public void Dispose()
        {
            _parada.Cancel();
            _escuta.Stop();
        }
    }

    // Com acentos o corpo viaja em base64 (ou quoted-printable): separa os cabeçalhos e decodifica como um leitor de e-mail faria.
    private static string CorpoDecodificado(string dados)
    {
        var linhas = dados.Replace("\r\n", "\n").Split('\n');
        var fim = Array.IndexOf(linhas, "");
        var cabecalhos = string.Join("\n", linhas[..fim]);
        var corpo = string.Join("\n", linhas[(fim + 1)..]);

        if (cabecalhos.Contains("Content-Transfer-Encoding: base64", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8.GetString(Convert.FromBase64String(corpo.Replace("\n", "")));

        return Regex.Replace(corpo.Replace("=\n", ""), "=([0-9A-F]{2})",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
    }

    private static ApiFactory Fabrica(ServidorSmtpDeTeste smtp) => new(new()
    {
        ["SMTP_HOST"] = "127.0.0.1",
        ["SMTP_PORT"] = smtp.Porta.ToString(),
        ["SMTP_SSL"] = "false",
        ["SMTP_FROM"] = "nao-responda@locadora.teste",
        ["FRONTEND_URL"] = "https://app.locadora.teste",
        ["RATE_LIMIT_PER_MINUTE"] = "5000",
        ["REGISTRATION_LIMIT_PER_HOUR"] = "5000"
    });

    [TestMethod]
    public async Task EmailSaiPeloSmtp_OLinkRedefineASenha_EOLinkSoVaiParaQuemTemConta()
    {
        using var smtp = new ServidorSmtpDeTeste();
        using var fabrica = Fabrica(smtp);
        using var cliente = fabrica.CreateClient();
        var email = $"recupera-{Guid.NewGuid():N}@teste.local";
        var registro = await cliente.PostAsJsonAsync("/api/auth/registrar", new { nomeCompleto = "Empresa", email, senha = Senha, confirmarSenha = Senha });
        Assert.AreEqual(HttpStatusCode.OK, registro.StatusCode);

        // e-mail sem conta: a resposta é igual e nada é enviado
        var semConta = await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new { email = "ninguem@teste.local" });
        Assert.AreEqual(HttpStatusCode.Accepted, semConta.StatusCode);
        Assert.IsNull(await smtp.EsperarAsync(TimeSpan.FromSeconds(2)), "e-mail sem conta não pode receber mensagem");

        var pedido = await cliente.PostAsJsonAsync("/api/auth/esqueci-senha", new { email });
        Assert.AreEqual(HttpStatusCode.Accepted, pedido.StatusCode);

        var recebido = await smtp.EsperarAsync(TimeSpan.FromSeconds(15));
        Assert.IsNotNull(recebido, "o e-mail precisa chegar ao servidor SMTP");
        Assert.AreEqual(email, recebido.Para);
        Assert.AreEqual("nao-responda@locadora.teste", recebido.De);

        var texto = CorpoDecodificado(recebido.Dados);
        StringAssert.Contains(recebido.Dados, "Subject: ");
        var link = Regex.Match(texto, @"https://app\.locadora\.teste/redefinir-senha#email=(?<email>[^&\s]+)&token=(?<token>[A-Za-z0-9_\-]+)");
        Assert.IsTrue(link.Success, "o corpo precisa trazer o link com e-mail e token no fragmento:\n" + texto);

        // usa o link do e-mail para redefinir a senha
        var redefinir = await cliente.PostAsJsonAsync("/api/auth/redefinir-senha", new
        {
            email = Uri.UnescapeDataString(link.Groups["email"].Value),
            token = link.Groups["token"].Value,
            novaSenha = NovaSenha,
            confirmarNovaSenha = NovaSenha
        });
        Assert.IsTrue(redefinir.IsSuccessStatusCode, await redefinir.Content.ReadAsStringAsync());

        Assert.AreEqual(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = Senha })).StatusCode, "a senha antiga deixa de valer");
        Assert.AreEqual(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/auth/autenticar", new { email, senha = NovaSenha })).StatusCode);

        // o mesmo link não vale duas vezes
        var reuso = await cliente.PostAsJsonAsync("/api/auth/redefinir-senha", new
        {
            email, token = link.Groups["token"].Value, novaSenha = "Outra@123456", confirmarNovaSenha = "Outra@123456"
        });
        Assert.AreEqual(HttpStatusCode.BadRequest, reuso.StatusCode);
    }
}
