using LocadoraDeVeiculos.Core.Dominio.Compartilhado;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao
{
    public class Configuracao : EntidadeBase<Configuracao>
    {
        // Valores usados quando a empresa ainda não gravou uma configuração própria.
        public const decimal PrecoCombustivelPadrao = 5.50m;
        public const decimal ValorGarantiaPadrao = 1000m;

        public decimal PrecoCombustivel { get; set; }
        public decimal ValorGarantia { get; set; } = ValorGarantiaPadrao;

        protected Configuracao() { }

        public Configuracao(Guid empresaId, decimal precoCombustivel)
        {
            EmpresaId = empresaId;
            PrecoCombustivel = precoCombustivel;
        }

        public override void AtualizarRegistro(Configuracao registroEditado)
        {
            PrecoCombustivel = registroEditado.PrecoCombustivel;
            ValorGarantia = registroEditado.ValorGarantia;
        }
    }
}