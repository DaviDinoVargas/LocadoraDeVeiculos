using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

/// <summary>Números da proteção contra adivinhação de senha (S10/S28).</summary>
public static class PoliticaDeBloqueio
{
    /// <summary>Erros seguidos que travam a conta inteira, vindos de qualquer lugar (força bruta distribuída).</summary>
    public const int TentativasPorConta = 10;

    /// <summary>Erros que travam só o par (conta, IP): um atacante sozinho para aqui, antes de trancar o dono para fora.</summary>
    public const int TentativasPorContaEIp = 5;

    public static readonly TimeSpan Janela = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Limite de erros de senha por par (conta, IP). Um atacante em um IP esgota o próprio limite em 5 erros e é barrado sem
/// somar mais nenhum erro à conta; o dono, vindo de outro IP, continua entrando. Só quem usa 2 ou mais IPs consegue chegar
/// aos 10 erros que travam a conta inteira (<see cref="PoliticaDeBloqueio.TentativasPorConta"/>).
/// Em memória e por instância da API; a chave é o e-mail digitado (exista a conta ou não), para não revelar quem tem conta.
/// </summary>
public sealed class LimitadorDeLoginPorConta
{
    private readonly Dictionary<string, List<DateTime>> _falhas = [];
    private readonly object _trava = new();
    private readonly TimeProvider _relogio;

    public LimitadorDeLoginPorConta(TimeProvider? relogio = null) => _relogio = relogio ?? TimeProvider.System;

    /// <summary>Este par já gastou os erros da janela? Sem IP conhecido não há o que separar: não bloqueia.</summary>
    public bool EstaBloqueado(string email, string? ip)
    {
        if (string.IsNullOrEmpty(ip))
            return false;

        var chave = Chave(email, ip);
        var agora = _relogio.GetUtcNow().UtcDateTime;

        lock (_trava)
        {
            if (!_falhas.TryGetValue(chave, out var momentos))
                return false;

            momentos.RemoveAll(t => agora - t >= PoliticaDeBloqueio.Janela);

            if (momentos.Count == 0)
            {
                _falhas.Remove(chave);
                return false;
            }

            return momentos.Count >= PoliticaDeBloqueio.TentativasPorContaEIp;
        }
    }

    public void RegistrarFalha(string email, string? ip)
    {
        if (string.IsNullOrEmpty(ip))
            return;

        var chave = Chave(email, ip);
        var agora = _relogio.GetUtcNow().UtcDateTime;

        lock (_trava)
        {
            if (_falhas.Count > 10_000)
                foreach (var k in new List<string>(_falhas.Keys))
                    if (_falhas[k].TrueForAll(t => agora - t >= PoliticaDeBloqueio.Janela))
                        _falhas.Remove(k);

            if (!_falhas.TryGetValue(chave, out var momentos))
                _falhas[chave] = momentos = [];

            momentos.RemoveAll(t => agora - t >= PoliticaDeBloqueio.Janela);
            momentos.Add(agora);
        }
    }

    /// <summary>Login certo: zera os erros do par.</summary>
    public void Zerar(string email, string? ip)
    {
        if (string.IsNullOrEmpty(ip))
            return;

        lock (_trava)
            _falhas.Remove(Chave(email, ip));
    }

    private static string Chave(string email, string ip) => $"{email.Trim().ToUpperInvariant()}|{Origem(ip)}";

    /// <summary>IPv4 vale inteiro; IPv6 vale pelo prefixo /64 (quem recebe um /64 tem 2^64 endereços para trocar a cada pedido).</summary>
    internal static string Origem(string ip)
    {
        if (!IPAddress.TryParse(ip, out var endereco))
            return ip;

        if (endereco.IsIPv4MappedToIPv6)
            endereco = endereco.MapToIPv4();

        if (endereco.AddressFamily != AddressFamily.InterNetworkV6)
            return endereco.ToString();

        var bytes = endereco.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }
}
