using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

/// <summary>
/// Reduz dados pessoais antes de irem para o log: o suficiente para correlacionar um incidente,
/// não o bastante para identificar a pessoa.
/// </summary>
public static class MascaramentoDeDados
{
    public static string MascararEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(vazio)";

        email = Sanear(email);

        var arroba = email.IndexOf('@');

        if (arroba < 1)
            return "***";

        return $"{email[0]}***{email[arroba..]}";
    }

    /// <summary>
    /// Entrada do usuário vai para o log: caracteres de controle e separadores de linha (inclusive Unicode) são removidos, senão um
    /// e-mail com quebra de linha forjaria linhas falsas no log. Também limita o tamanho.
    /// </summary>
    public static string Sanear(string? texto, int maximo = 120)
    {
        if (string.IsNullOrEmpty(texto))
            return string.Empty;

        var limpo = new System.Text.StringBuilder(Math.Min(texto.Length, maximo));

        foreach (var c in texto)
        {
            if (char.IsControl(c) ||
                System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator)
                continue;

            limpo.Append(c);

            if (limpo.Length >= maximo)
                break;
        }

        return limpo.ToString();
    }
}
