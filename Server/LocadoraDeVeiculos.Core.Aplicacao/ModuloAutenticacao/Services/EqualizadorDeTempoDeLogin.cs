using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Identity;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

/// <summary>
/// Faz o login de um e-mail que não existe custar o mesmo que o de um que existe (S20): o PBKDF2 do Identity é caro de
/// propósito e, sem isto, só o caminho "usuário existe" paga esse custo, o que denuncia quem tem conta.
/// </summary>
public static class EqualizadorDeTempoDeLogin
{
    private static readonly Usuario UsuarioFalso = new() { UserName = "descartavel" };

    // Hash por IPasswordHasher (um por configuração de iterações): calculado uma vez, depois só verificado.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<IPasswordHasher<Usuario>, string> Hashes = new();

    public static void GastarCustoDeHash(IPasswordHasher<Usuario> hasher, string senhaInformada)
    {
        var hash = Hashes.GetOrAdd(hasher, h => h.HashPassword(UsuarioFalso, "senha-descartavel-sem-uso-real"));

        hasher.VerifyHashedPassword(UsuarioFalso, hash, senhaInformada ?? string.Empty);
    }
}
