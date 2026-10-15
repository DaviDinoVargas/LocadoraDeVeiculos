using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using LocadoraDeVeiculos.Infraestrutura.Orm.orm.Compartilhado;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;

/// <summary>
/// Guarda cifrados (AES-256-GCM, a mesma chave versionada da biometria) os dois segredos do segundo fator que o Identity grava em AspNetUserTokens:
/// a chave do autenticador (TOTP) e os códigos de recuperação. Sem isto, quem lê uma cópia do banco enxerga a semente de cada conta e gera os
/// códigos de 6 dígitos sozinho, e os códigos de recuperação valem como segundo fator. O valor cifrado é amarrado à pessoa e ao tipo do segredo
/// (dado associado do GCM): copiar a linha de uma conta para outra não funciona.
///
/// Valores gravados antes desta mudança (texto puro, sem o prefixo) continuam sendo lidos; na próxima gravação já saem cifrados.
/// </summary>
public sealed class UsuarioStoreComSegredosCifrados(
    LocadoraDeVeiculosDbContext contexto,
    CifradorFacial cifrador,
    IdentityErrorDescriber? descritorDeErros = null)
    : UserStore<Usuario, Cargo, LocadoraDeVeiculosDbContext, Guid>(contexto, descritorDeErros)
{
    public const string Prefixo = "enc1:";

    // Nomes que o Identity usa para o "provedor" interno e para os dois tokens secretos do segundo fator.
    private const string ProvedorInterno = "[AspNetUserStore]";
    private static readonly HashSet<string> TokensSecretos = new(StringComparer.Ordinal) { "AuthenticatorKey", "RecoveryCodes" };

    public override Task SetTokenAsync(Usuario user, string loginProvider, string name, string? value, CancellationToken cancellationToken)
        => base.SetTokenAsync(user, loginProvider, name, EhSecreto(loginProvider, name) ? Proteger(user, name, value) : value, cancellationToken);

    public override async Task<string?> GetTokenAsync(Usuario user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        var valor = await base.GetTokenAsync(user, loginProvider, name, cancellationToken);

        return EhSecreto(loginProvider, name) ? Revelar(user, name, valor) : valor;
    }

    private static bool EhSecreto(string loginProvider, string name) => loginProvider == ProvedorInterno && TokensSecretos.Contains(name);

    private static byte[] DadoAssociado(Usuario usuario, string nome) => Encoding.UTF8.GetBytes($"{usuario.Id}|{nome}");

    private string? Proteger(Usuario usuario, string nome, string? valor)
    {
        if (string.IsNullOrEmpty(valor))
            return valor;

        return Prefixo + Convert.ToBase64String(cifrador.Cifrar(Encoding.UTF8.GetBytes(valor), Encoding.UTF8.GetString(DadoAssociado(usuario, nome))));
    }

    private string? Revelar(Usuario usuario, string nome, string? valor)
    {
        if (string.IsNullOrEmpty(valor) || !valor.StartsWith(Prefixo, StringComparison.Ordinal))
            return valor;   // gravado antes da cifra: continua valendo até a próxima gravação

        try
        {
            var puro = cifrador.Decifrar(Convert.FromBase64String(valor[Prefixo.Length..]), Encoding.UTF8.GetString(DadoAssociado(usuario, nome)));

            return Encoding.UTF8.GetString(puro);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;    // adulterado, de outra conta ou com chave que saiu da lista: o segundo fator falha FECHADO (como se não houvesse chave)
        }
    }
}
