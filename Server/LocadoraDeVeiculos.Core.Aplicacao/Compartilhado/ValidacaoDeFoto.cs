using FluentValidation;
using System;
using System.Text.RegularExpressions;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

/// <summary>
/// A foto do veículo vai para um &lt;img src&gt;. Aceita só uma URL https ou uma imagem embutida pequena; recusa
/// esquemas executáveis (javascript:), de arquivo local (file:), HTTP sem TLS e imagens enormes que pesariam no banco e na tela (S31).
/// </summary>
public static partial class ValidacaoDeFoto
{
    public const int TamanhoMaximoDaUrl = 2048;
    public const int TamanhoMaximoDaImagemEmbutida = 300_000; // ~225 KB de imagem em base64

    [GeneratedRegex(@"^data:image/(png|jpeg|jpg|webp|gif);base64,[A-Za-z0-9+/=]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ImagemEmbutida();

    public static bool FotoValida(string? foto)
    {
        if (string.IsNullOrWhiteSpace(foto))
            return true;

        if (foto.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return foto.Length <= TamanhoMaximoDaImagemEmbutida && ImagemEmbutida().IsMatch(foto);

        return foto.Length <= TamanhoMaximoDaUrl
            && Uri.TryCreate(foto, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo);
    }

    public static IRuleBuilderOptions<T, string?> FotoDoVeiculoValida<T>(this IRuleBuilder<T, string?> regra) =>
        regra.Must(FotoValida)
             .WithMessage("A foto deve ser uma URL https (até 2048 caracteres) ou uma imagem PNG/JPEG/WebP/GIF embutida de até 300 KB.");
}
