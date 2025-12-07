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

    public static void EnviarCookie(HttpResponse response, RefreshToken token)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = token.ExpiraEmUtc
        };

        response.Cookies.Append(nome, token.TokenHash, options);
    }

    public static void LimparCookie(HttpResponse response)
    {
        response.Cookies.Delete(nome, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None
        });
    }

    public static string? Get(HttpRequest request)
    {
        return request.Cookies[nome];
    }
}