using FluentValidation;

namespace LocadoraDeVeiculos.Core.Aplicacao.Compartilhado;

/// <summary>
/// Conferência dos dígitos verificadores de CPF e CNPJ. O formato (máscara) é validado à parte pelos
/// validators; aqui só se confere se o número pode existir. Espelha <c>shared/validators/documentos.ts</c>
/// do front, para que um cliente que contorne a tela não grave documentos impossíveis.
/// </summary>
public static class ValidacaoDeDocumentos
{
    private static readonly int[] PesosCpf1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCpf2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpj1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] PesosCnpj2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    public static bool CpfValido(string? valor)
    {
        var cpf = SomenteDigitos(valor);

        if (cpf.Length != 11 || TodosIguais(cpf))
            return false;

        return Digito(cpf, PesosCpf1) == cpf[9] - '0' && Digito(cpf, PesosCpf2) == cpf[10] - '0';
    }

    public static bool CnpjValido(string? valor)
    {
        var cnpj = SomenteDigitos(valor);

        if (cnpj.Length != 14 || TodosIguais(cnpj))
            return false;

        return Digito(cnpj, PesosCnpj1) == cnpj[12] - '0' && Digito(cnpj, PesosCnpj2) == cnpj[13] - '0';
    }

    /// <summary>Campo vazio é problema do <c>NotEmpty</c>; esta regra só fala dos dígitos.</summary>
    public static IRuleBuilderOptions<T, string> DocumentoCpfValido<T>(this IRuleBuilder<T, string> regra) =>
        regra.Must(valor => string.IsNullOrEmpty(valor) || CpfValido(valor))
             .WithMessage("O campo {PropertyName} não é um CPF válido: confira os números.");

    public static IRuleBuilderOptions<T, string> DocumentoCnpjValido<T>(this IRuleBuilder<T, string> regra) =>
        regra.Must(valor => string.IsNullOrEmpty(valor) || CnpjValido(valor))
             .WithMessage("O campo {PropertyName} não é um CNPJ válido: confira os números.");

    private static string SomenteDigitos(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    private static bool TodosIguais(string digitos) => digitos.All(c => c == digitos[0]);

    private static int Digito(string digitos, int[] pesos)
    {
        var soma = 0;

        for (var i = 0; i < pesos.Length; i++)
            soma += (digitos[i] - '0') * pesos[i];

        var resto = soma % 11;

        return resto < 2 ? 0 : 11 - resto;
    }
}
