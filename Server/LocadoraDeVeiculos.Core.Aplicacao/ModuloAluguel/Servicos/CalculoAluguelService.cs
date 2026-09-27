using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAluguel.Servicos;

/// <summary>
/// Calcula o valor previsto de um aluguel no servidor, a partir do plano de cobrança do
/// grupo do automóvel e das taxas/serviços selecionados. O valor nunca deve vir do cliente:
/// ele só decide QUAIS taxas/serviços quer, não QUANTO isso custa.
/// </summary>
public static class CalculoAluguelService
{
    public static int CalcularDiasContratados(DateTimeOffset dataSaida, DateTimeOffset dataRetornoPrevisto)
    {
        var dias = (int)Math.Ceiling((dataRetornoPrevisto - dataSaida).TotalDays);

        return Math.Max(1, dias);
    }

    public static decimal CalcularValorPrevisto(
        PlanoCobranca planoCobranca,
        DateTimeOffset dataSaida,
        DateTimeOffset dataRetornoPrevisto,
        IEnumerable<TaxaServico> taxasServicos)
    {
        var dias = CalcularDiasContratados(dataSaida, dataRetornoPrevisto);

        var valorDiarias = planoCobranca.PrecoDiaria * dias;
        var valorTaxas = taxasServicos.Sum(t => t.CalcularValor(dias));

        return valorDiarias + valorTaxas;
    }
}
