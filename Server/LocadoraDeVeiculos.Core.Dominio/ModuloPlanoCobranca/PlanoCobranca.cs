using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca
{
    public class PlanoCobranca : EntidadeBase<PlanoCobranca>
    {
        public Guid GrupoAutomovelId { get; set; }
        public string Nome { get; set; }

        public decimal PrecoDiaria { get; set; }
        public decimal PrecoPorKm { get; set; }
        public int KmLivreLimite { get; set; }

        protected PlanoCobranca() { }

        public PlanoCobranca(Guid grupoId, Guid empresaId, string nome,
            decimal precoDiaria, decimal precoPorKm, int kmLivreLimite)
        {
            GrupoAutomovelId = grupoId;
            EmpresaId = empresaId;
            Nome = nome;
            PrecoDiaria = precoDiaria;
            PrecoPorKm = precoPorKm;
            KmLivreLimite = kmLivreLimite;
        }

        public override void AtualizarRegistro(PlanoCobranca registroEditado)
        {
            GrupoAutomovelId = registroEditado.GrupoAutomovelId;
            Nome = registroEditado.Nome;
            PrecoDiaria = registroEditado.PrecoDiaria;
            PrecoPorKm = registroEditado.PrecoPorKm;
            KmLivreLimite = registroEditado.KmLivreLimite;
        }
    }
}
