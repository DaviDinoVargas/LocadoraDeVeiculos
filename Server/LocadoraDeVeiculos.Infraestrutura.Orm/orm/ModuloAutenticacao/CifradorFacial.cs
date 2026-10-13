using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;

/// <summary>
/// Cifra os landmarks faciais com AES-256-GCM. A chave fica fora do banco (FACE_ENCRYPTION_KEYS) e é
/// versionada para permitir rotação: o primeiro byte do conteúdo diz com qual chave ele foi cifrado,
/// novas amostras usam sempre a chave de maior id e as antigas continuam legíveis enquanto a chave
/// antiga permanecer na lista.
///
/// Formato do conteúdo: [id da chave: 1 byte][nonce: 12][tag: 16][texto cifrado].
/// O identificador da pessoa entra como dado associado (AAD): uma linha copiada para outra pessoa
/// deixa de decifrar.
/// </summary>
public sealed class CifradorFacial
{
    public const string ChaveDeConfiguracao = "FACE_ENCRYPTION_KEYS";

    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;
    private const int TamanhoChave = 32;

    // Só vale em desenvolvimento, para o projeto rodar sem configurar nada. Está no repositório,
    // portanto não protege dados reais.
    private const string ChaveDeDesenvolvimento = "locadora-dev-chave-facial-nao-use-em-producao";

    private readonly Dictionary<byte, byte[]> _chaves;
    private readonly byte _idAtivo;

    public CifradorFacial(IReadOnlyDictionary<byte, byte[]> chaves)
    {
        if (chaves.Count == 0)
            throw new ArgumentException("Pelo menos uma chave de cifra facial é necessária.");

        foreach (var (id, chave) in chaves)
            if (chave.Length != TamanhoChave)
                throw new ArgumentException($"A chave facial {id} precisa ter {TamanhoChave} bytes (AES-256).");

        _chaves = new Dictionary<byte, byte[]>(chaves);
        _idAtivo = chaves.Keys.Max();
    }

    /// <summary>
    /// Lê "FACE_ENCRYPTION_KEYS" no formato "1:&lt;base64&gt;;2:&lt;base64&gt;" (cada chave com 32 bytes).
    /// Em desenvolvimento, sem a variável, usa uma chave derivada de um texto fixo; fora dele a
    /// aplicação não sobe sem chave.
    /// </summary>
    public static CifradorFacial DaConfiguracao(IConfiguration configuration)
    {
        var texto = configuration[ChaveDeConfiguracao];

        if (string.IsNullOrWhiteSpace(texto))
        {
            var desenvolvimento = string.Equals(
                configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);

            if (!desenvolvimento)
                throw new InvalidOperationException(
                    $"\"{ChaveDeConfiguracao}\" precisa ser definida (ex.: \"1:<32 bytes em base64>\") fora do ambiente de desenvolvimento.");

            return new CifradorFacial(new Dictionary<byte, byte[]>
            {
                [1] = SHA256.HashData(Encoding.UTF8.GetBytes(ChaveDeDesenvolvimento))
            });
        }

        var chaves = new Dictionary<byte, byte[]>();

        foreach (var item in texto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var partes = item.Split(':', 2);

            if (partes.Length != 2 || !byte.TryParse(partes[0], out var id) || id == 0)
                throw new InvalidOperationException($"\"{ChaveDeConfiguracao}\" tem um item inválido; use \"<id 1-255>:<base64>\".");

            byte[] bytes;
            try { bytes = Convert.FromBase64String(partes[1]); }
            catch (FormatException) { throw new InvalidOperationException($"\"{ChaveDeConfiguracao}\": a chave {id} não está em base64."); }

            if (!chaves.TryAdd(id, bytes))
                throw new InvalidOperationException($"\"{ChaveDeConfiguracao}\": o id {id} aparece duas vezes.");
        }

        return new CifradorFacial(chaves);
    }

    public byte[] Cifrar(byte[] textoPuro, string pessoaId)
    {
        var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce);
        var cifrado = new byte[textoPuro.Length];
        var tag = new byte[TamanhoTag];

        using var aes = new AesGcm(_chaves[_idAtivo], TamanhoTag);
        aes.Encrypt(nonce, textoPuro, cifrado, tag, Encoding.UTF8.GetBytes(pessoaId));

        var resultado = new byte[1 + TamanhoNonce + TamanhoTag + cifrado.Length];
        resultado[0] = _idAtivo;
        nonce.CopyTo(resultado, 1);
        tag.CopyTo(resultado, 1 + TamanhoNonce);
        cifrado.CopyTo(resultado, 1 + TamanhoNonce + TamanhoTag);

        return resultado;
    }

    public byte[] Decifrar(byte[] conteudo, string pessoaId)
    {
        if (conteudo.Length < 1 + TamanhoNonce + TamanhoTag)
            throw new CryptographicException("Conteúdo facial corrompido.");

        if (!_chaves.TryGetValue(conteudo[0], out var chave))
            throw new CryptographicException($"A chave facial {conteudo[0]} não está mais configurada.");

        var nonce = conteudo.AsSpan(1, TamanhoNonce);
        var tag = conteudo.AsSpan(1 + TamanhoNonce, TamanhoTag);
        var cifrado = conteudo.AsSpan(1 + TamanhoNonce + TamanhoTag);
        var textoPuro = new byte[cifrado.Length];

        using var aes = new AesGcm(chave, TamanhoTag);
        aes.Decrypt(nonce, cifrado, tag, textoPuro, Encoding.UTF8.GetBytes(pessoaId));

        return textoPuro;
    }
}
