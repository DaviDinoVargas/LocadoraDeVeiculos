using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloCupom
{
    public class Cupom : EntidadeBase<Cupom>
    {
        public string Codigo { get; set; }
        public string Descricao { get; set; }
        public TipoDesconto TipoDesconto { get; set; }
        public decimal ValorDesconto { get; set; }
        public DateTimeOffset ValidoAte { get; set; }
        public int? LimiteUsos { get; set; }
        public int UsosAtuais { get; set; }
        public bool Ativo { get; set; } = true;

        public Guid? ParceiroId { get; set; }
        public Parceiro? Parceiro { get; set; }

        protected Cupom() { }

        public Cupom(
            string codigo,
            string descricao,
            TipoDesconto tipoDesconto,
            decimal valorDesconto,
            DateTimeOffset validoAte,
            int? limiteUsos,
            Guid? parceiroId)
        {
            Codigo = codigo.ToUpperInvariant();
            Descricao = descricao;
            TipoDesconto = tipoDesconto;
            ValorDesconto = valorDesconto;
            ValidoAte = validoAte;
            LimiteUsos = limiteUsos;
            ParceiroId = parceiroId;
        }

        public override void AtualizarRegistro(Cupom registroEditado)
        {
            Codigo = registroEditado.Codigo;
            Descricao = registroEditado.Descricao;
            TipoDesconto = registroEditado.TipoDesconto;
            ValorDesconto = registroEditado.ValorDesconto;
            ValidoAte = registroEditado.ValidoAte;
            LimiteUsos = registroEditado.LimiteUsos;
            ParceiroId = registroEditado.ParceiroId;
            Ativo = registroEditado.Ativo;
        }

        /// <summary>
        /// Um cupom só pode ser usado se estiver ativo, dentro da validade e (quando houver
        /// limite) ainda não tiver esgotado os usos. Checagem central usada em qualquer lugar
        /// que aplique um cupom — nunca confiar apenas em "o código existe".
        /// </summary>
        public bool EstaValido(DateTimeOffset agora)
        {
            if (!Ativo) return false;
            if (ValidoAte < agora) return false;
            if (LimiteUsos.HasValue && UsosAtuais >= LimiteUsos.Value) return false;

            return true;
        }

        public decimal CalcularDesconto(decimal valorBase)
        {
            if (valorBase <= 0) return 0;

            var desconto = TipoDesconto == TipoDesconto.Percentual
                ? valorBase * (ValorDesconto / 100m)
                : ValorDesconto;

            // Nunca desconta mais do que o valor total (não faz sentido um aluguel "negativo").
            return Math.Min(desconto, valorBase);
        }

        public void RegistrarUso()
        {
            UsosAtuais++;
        }
    }

    public enum TipoDesconto
    {
        Percentual = 1,
        ValorFixo = 2
    }
}
