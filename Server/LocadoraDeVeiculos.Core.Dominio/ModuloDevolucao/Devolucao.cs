using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao
{
    public class Devolucao : EntidadeBase<Devolucao>
    {
        public Guid AluguelId { get; set; }
        public Aluguel? Aluguel { get; set; }

        public DateTimeOffset DataDevolucao { get; set; }
        public decimal QuilometragemFinal { get; set; }
        public decimal CombustivelNoTanque { get; set; }

        // Valores calculados
        public decimal ValorMultas { get; set; }
        public decimal ValorAdicionalCombustivel { get; set; }
        public decimal ValorTotal { get; set; }

        // Nível de combustível (para cálculo)
        public NivelCombustivel NivelCombustivel { get; set; }

        protected Devolucao() { }

        public Devolucao(
            Guid aluguelId,
            DateTimeOffset dataDevolucao,
            decimal quilometragemFinal,
            decimal combustivelNoTanque,
            NivelCombustivel nivelCombustivel)
        {
            AluguelId = aluguelId;
            DataDevolucao = dataDevolucao;
            QuilometragemFinal = quilometragemFinal;
            CombustivelNoTanque = combustivelNoTanque;
            NivelCombustivel = nivelCombustivel;
        }

        public override void AtualizarRegistro(Devolucao registroEditado)
        {
            DataDevolucao = registroEditado.DataDevolucao;
            QuilometragemFinal = registroEditado.QuilometragemFinal;
            CombustivelNoTanque = registroEditado.CombustivelNoTanque;
            NivelCombustivel = registroEditado.NivelCombustivel;
            ValorMultas = registroEditado.ValorMultas;
            ValorAdicionalCombustivel = registroEditado.ValorAdicionalCombustivel;
            ValorTotal = registroEditado.ValorTotal;
        }

        // Método para calcular os valores (será chamado no handler)
        public void CalcularValores(
            decimal precoCombustivel,
            decimal capacidadeTanque,
            decimal quilometragemInicial,
            DateTimeOffset dataRetornoPrevisto,
            decimal valorPrevistoAluguel)
        {
            CalcularMulta(dataRetornoPrevisto, valorPrevistoAluguel);
            CalcularAdicionalCombustivel(precoCombustivel, capacidadeTanque);
            CalcularValorTotal(valorPrevistoAluguel);
        }

        private void CalcularMulta(DateTimeOffset dataRetornoPrevisto, decimal valorPrevistoAluguel)
        {
            if (DataDevolucao > dataRetornoPrevisto)
            {
                var diasAtraso = (DataDevolucao - dataRetornoPrevisto).Days;
                // Multa de 10% do valor do aluguel (conforme requisitos)
                ValorMultas = valorPrevistoAluguel * 0.10m;
            }
            else
            {
                ValorMultas = 0;
            }
        }

        private void CalcularAdicionalCombustivel(decimal precoCombustivel, decimal capacidadeTanque)
        {
            decimal percentualCombustivel = (CombustivelNoTanque / capacidadeTanque) * 100;

            if (percentualCombustivel < (int)NivelCombustivel)
            {
                // Cobrar pela diferença
                decimal litrosFaltantes = capacidadeTanque - CombustivelNoTanque;
                ValorAdicionalCombustivel = litrosFaltantes * precoCombustivel;
            }
            else
            {
                ValorAdicionalCombustivel = 0;
            }
        }

        private void CalcularValorTotal(decimal valorPrevistoAluguel)
        {
            // Soma do valor do aluguel + multas + adicional de combustível
            ValorTotal = valorPrevistoAluguel + ValorMultas + ValorAdicionalCombustivel;
        }
    }

    public enum NivelCombustivel
    {
        Vazio = 0,
        UmQuarto = 25,
        Meio = 50,
        TresQuartos = 75,
        Cheio = 100
    }
}