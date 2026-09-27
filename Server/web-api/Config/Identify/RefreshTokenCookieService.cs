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

    public static void EnviarCookie(HttpResponse response, string tokenBruto, DateTime expiraEmUtc)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
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
            SameSite = SameSiteMode.Lax
        });
    }

    public static string? Get(HttpRequest request)
    {
        return request.Cookies[nome];
    }
}