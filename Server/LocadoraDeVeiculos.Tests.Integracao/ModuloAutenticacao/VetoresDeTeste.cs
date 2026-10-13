using LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;
using System;
using System.Linq;

namespace LocadoraDeVeiculos.Tests.Integracao.ModuloAutenticacao;

/// <summary>Vetores de rosto de mentira no tamanho do método atual (128 números), para testar o armazenamento.</summary>
public static class VetoresDeTeste
{
    /// <summary>Vetor de 128 números que começa pelos valores informados e segue com zeros.</summary>
    public static double[] V(params double[] inicio) =>
        [.. inicio.Concat(Enumerable.Repeat(0.0, ArmazenamentoFacialEmOrm.TamanhoDoVetor - inicio.Length))];

    /// <summary>Formato do método anterior (geometria dos 468 pontos, 1404 números): não serve mais.</summary>
    public static double[] FormatoAntigo(double valor = 0.1) => [.. Enumerable.Repeat(valor, 1404)];
}
