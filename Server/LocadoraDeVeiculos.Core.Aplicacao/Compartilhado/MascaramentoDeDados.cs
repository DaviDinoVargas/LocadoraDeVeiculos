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

        var arroba = email.IndexOf('@');

        if (arroba < 1)
            return "***";

        return $"{email[0]}***{email[arroba..]}";
    }
}
