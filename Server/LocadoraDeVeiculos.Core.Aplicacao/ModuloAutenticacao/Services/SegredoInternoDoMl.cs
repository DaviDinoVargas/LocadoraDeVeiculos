using Microsoft.Extensions.Configuration;
using System;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.Services;

/// <summary>
/// Segredo compartilhado com o serviço de ML (X-Internal-Token). Em desenvolvimento aceita o valor
/// padrão público por conveniência; fora dele a aplicação não sobe sem um segredo próprio e longo,
/// porque o padrão está no repositório e daria acesso ao módulo facial (cadastrar, verificar e apagar rostos).
/// </summary>
public static class SegredoInternoDoMl
{
    public const string ChaveDeConfiguracao = "MlApi:InternalToken";
    public const string ValorDeDesenvolvimento = "dev-only-shared-secret-troque-em-producao";
    public const int TamanhoMinimo = 32;

    public static string Resolver(IConfiguration configuration)
    {
        var valor = configuration[ChaveDeConfiguracao]?.Trim();
        var desenvolvimento = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);

        if (desenvolvimento)
            return string.IsNullOrEmpty(valor) ? ValorDeDesenvolvimento : valor;

        if (string.IsNullOrEmpty(valor) || valor == ValorDeDesenvolvimento)
            throw new InvalidOperationException(
                $"\"{ChaveDeConfiguracao}\" (ML_INTERNAL_TOKEN) precisa ser definido, com valor diferente do de desenvolvimento, fora do ambiente de desenvolvimento.");

        if (valor.Length < TamanhoMinimo)
            throw new InvalidOperationException(
                $"\"{ChaveDeConfiguracao}\" precisa ter pelo menos {TamanhoMinimo} caracteres.");

        return valor;
    }
}
