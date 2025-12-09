using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico
{
    public class TaxaServico : EntidadeBase<TaxaServico>
    {
        public string Nome { get; set; }
        public decimal Preco { get; set; }
        public TipoCalculo TipoCalculo { get; set; }

        protected TaxaServico() { }

        public TaxaServico(
            string nome,
            decimal preco,
            TipoCalculo tipoCalculo)
        {
            Nome = nome;
            Preco = preco;
            TipoCalculo = tipoCalculo;
        }

        public override void AtualizarRegistro(TaxaServico registroEditado)
        {
            Nome = registroEditado.Nome;
            Preco = registroEditado.Preco;
            TipoCalculo = registroEditado.TipoCalculo;
        }

        public decimal CalcularValor(int dias = 1)
        {
            return TipoCalculo == TipoCalculo.Fixo ? Preco : Preco * dias;
        }
    }

    public enum TipoCalculo
    {
        Fixo = 1,
        Diario = 2
    }
}