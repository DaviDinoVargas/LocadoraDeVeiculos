using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom
{
    /// <summary>
    /// Um desafio é uma meta que, quando cumprida por um cliente (ex.: completar N aluguéis
    /// dentro de um período), libera o uso de um cupom de recompensa.
    /// </summary>
    public class DesafioCupom : EntidadeBase<DesafioCupom>
    {
        public string Nome { get; set; }
        public string Descricao { get; set; }
        public int MetaQuantidadeAlugueis { get; set; }
        public int PeriodoDias { get; set; }
        public bool Ativo { get; set; } = true;

        public Guid CupomRecompensaId { get; set; }
        public Cupom? CupomRecompensa { get; set; }

        protected DesafioCupom() { }

        public DesafioCupom(
            string nome,
            string descricao,
            int metaQuantidadeAlugueis,
            int periodoDias,
            Guid cupomRecompensaId)
        {
            Nome = nome;
            Descricao = descricao;
            MetaQuantidadeAlugueis = metaQuantidadeAlugueis;
            PeriodoDias = periodoDias;
            CupomRecompensaId = cupomRecompensaId;
        }

        public override void AtualizarRegistro(DesafioCupom registroEditado)
        {
            Nome = registroEditado.Nome;
            Descricao = registroEditado.Descricao;
            MetaQuantidadeAlugueis = registroEditado.MetaQuantidadeAlugueis;
            PeriodoDias = registroEditado.PeriodoDias;
            CupomRecompensaId = registroEditado.CupomRecompensaId;
            Ativo = registroEditado.Ativo;
        }
    }
}
