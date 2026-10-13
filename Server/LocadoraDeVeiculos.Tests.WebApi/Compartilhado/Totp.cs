using System.Security.Cryptography;

namespace LocadoraDeVeiculos.Tests.WebApi.Compartilhado;

/// <summary>TOTP (RFC 6238) para o teste fazer o papel do aplicativo autenticador.</summary>
public static class Totp
{
    public static string Codigo(string chaveEmBase32)
    {
        var passo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var contador = BitConverter.GetBytes(passo);
        if (BitConverter.IsLittleEndian) Array.Reverse(contador);
        var hash = HMACSHA1.HashData(DecodificarBase32(chaveEmBase32), contador);
        var o = hash[^1] & 0x0F;
        var numero = ((hash[o] & 0x7F) << 24) | (hash[o + 1] << 16) | (hash[o + 2] << 8) | hash[o + 3];

        return (numero % 1_000_000).ToString("D6");
    }

    private static byte[] DecodificarBase32(string texto)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var valor = 0;
        var saida = new List<byte>();

        foreach (var c in texto.ToUpperInvariant().Where(alfabeto.Contains))
        {
            valor = (valor << 5) | alfabeto.IndexOf(c);
            bits += 5;

            if (bits >= 8)
            {
                saida.Add((byte)((valor >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. saida];
    }
}
