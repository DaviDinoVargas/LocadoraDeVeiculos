using System;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.jwt;

/// <summary>
/// Regras mínimas para a chave que assina os access tokens (HS256).
/// </summary>
public static class ChaveJwt
{
    // 32 caracteres ASCII = 256 bits, o tamanho do hash do HS256. Abaixo disso a chave deixa de
    // acrescentar segurança e passa a ser o elo mais fraco da assinatura.
    public const int TamanhoMinimo = 32;

    public static string Validar(string? chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new ArgumentException("Não foi possível obter a chave de assinatura de tokens.");

        if (chave.Length < TamanhoMinimo)
            throw new ArgumentException(
                $"A chave de assinatura de tokens (JWT_GENERATION_KEY) precisa ter pelo menos {TamanhoMinimo} caracteres.");

        return chave;
    }
}
