using LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

/// <summary>
/// Fila em memória dos pedidos de "esqueci minha senha". O endpoint só enfileira e responde na hora, igual para e-mail
/// existente ou não: o trabalho de verdade (procurar a conta, gerar o token, mandar o e-mail) acontece depois, então nem a
/// resposta nem o tempo dela revelam quem tem conta. Também limita pedidos por e-mail (3 por hora) para não virar
/// disparador de spam contra a caixa de terceiros.
/// </summary>
public sealed class FilaDeRecuperacaoDeSenha
{
    public const int MaximoPorHora = 3;

    private readonly Channel<string> _canal = Channel.CreateBounded<string>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    private readonly Dictionary<string, List<DateTime>> _pedidos = [];
    private readonly object _trava = new();
    private readonly TimeProvider _relogio;

    public FilaDeRecuperacaoDeSenha(TimeProvider? relogio = null) => _relogio = relogio ?? TimeProvider.System;

    public ChannelReader<string> Leitor => _canal.Reader;

    /// <summary>Aceita o pedido (ou o descarta em silêncio se passou do limite do e-mail). Devolve se foi aceito.</summary>
    public bool Enfileirar(string email)
    {
        var chave = Chave(email);
        var agora = _relogio.GetUtcNow().UtcDateTime;

        lock (_trava)
        {
            if (_pedidos.Count > 5000)
                foreach (var k in new List<string>(_pedidos.Keys))
                    if (_pedidos[k].TrueForAll(t => agora - t > TimeSpan.FromHours(1)))
                        _pedidos.Remove(k);

            if (!_pedidos.TryGetValue(chave, out var momentos))
                _pedidos[chave] = momentos = [];

            momentos.RemoveAll(t => agora - t > TimeSpan.FromHours(1));

            if (momentos.Count >= MaximoPorHora)
                return false;

            momentos.Add(agora);
        }

        return _canal.Writer.TryWrite(email.Trim());
    }

    private static string Chave(string email) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
}

/// <summary>Procura a conta, gera o token de redefinição e manda o e-mail com o link. Nunca revela ao chamador se a conta existe.</summary>
public class RecuperacaoDeSenha(
    UserManager<Usuario> userManager,
    IEnviadorDeEmail enviador,
    IConfiguration configuration,
    ILogger<RecuperacaoDeSenha> logger)
{
    public static string CodificarToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodificarToken(string? codificado)
    {
        try
        {
            return string.IsNullOrWhiteSpace(codificado) ? null : Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(codificado));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task ProcessarPedidoAsync(string email, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByEmailAsync(email);

        // Sem conta, ou conta encerrada (bloqueio para sempre): nada é enviado e nada é dito a ninguém.
        if (usuario?.Email is null || EncerradorDeAcesso.ContaEncerrada(usuario))
        {
            logger.LogInformation("Pedido de recuperação de senha sem conta elegível ({Email}).", MascaramentoDeDados.MascararEmail(email));
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(usuario);
        var baseDoFront = (configuration["FRONTEND_URL"] ?? "http://localhost:4200").TrimEnd('/');

        // O token vai no fragmento (#): fragmento não é enviado ao servidor, não aparece em log de proxy nem em Referer.
        var link = $"{baseDoFront}/redefinir-senha#email={Uri.EscapeDataString(usuario.Email)}&token={CodificarToken(token)}";

        var corpo = $"""
            Olá,

            Recebemos um pedido para redefinir a senha da sua conta na LocadoraDeVeiculos.

            Para escolher uma nova senha, abra o link abaixo (vale por 24 horas):

            {link}

            Se não foi você, ignore este e-mail: sua senha continua a mesma.
            """;

        await enviador.EnviarAsync(usuario.Email, "Redefinição de senha", corpo, cancellationToken);
    }
}
