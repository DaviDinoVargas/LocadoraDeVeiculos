using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System;

namespace LocadoraDeVeiculos.WebApi.Config.Identify;

public static class RefreshTokenCookieService
{
    private static readonly string nome = "LocadoraDeVeiculos.RefreshToken";

    /// <summary>
    /// O navegador só envia o cookie às rotas de autenticação (rotacionar, sair...), não a cada chamada da API. O front e o nginx falam com a
    /// API por /api, então o caminho público é o mesmo do interno. Limpar o cookie precisa do MESMO Path, senão o navegador não o apaga.
    /// </summary>
    public const string Caminho = "/api/auth";

    public static void EnviarCookie(HttpResponse response, string tokenBruto, DateTime expiraEmUtc)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = Caminho,
            Expires = expiraEmUtc
        };

        // O cookie carrega o token BRUTO (o segredo em si), nunca o hash guardado no banco.
        response.Cookies.Append(nome, tokenBruto, options);
    }

    public static void LimparCookie(HttpResponse response)
    {
        response.Cookies.Delete(nome, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = Caminho
        });
    }

    public static string? Get(HttpRequest request)
    {
        return request.Cookies[nome];
    }
}