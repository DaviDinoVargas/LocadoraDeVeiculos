using LocadoraDeVeiculos.Core.Dominio.Compartilhado;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao
{
    public class Configuracao : EntidadeBase<Configuracao>
    {
        public decimal PrecoCombustivel { get; set; }
        public decimal ValorGarantia { get; set; } = 1000m; 

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