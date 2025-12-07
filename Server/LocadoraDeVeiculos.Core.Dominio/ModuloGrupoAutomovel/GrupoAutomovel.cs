using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel
{
    public class GrupoAutomovel : EntidadeBase<GrupoAutomovel>
    {
        public string Nome { get; set; }
        public string? Descricao { get; set; }

        protected GrupoAutomovel() { }

        public GrupoAutomovel(string nome, string? descricao = null)
        {
            Nome = nome;
            Descricao = descricao;
        }

        public override void AtualizarRegistro(GrupoAutomovel registroEditado)
        {
            Nome = registroEditado.Nome;
            Descricao = registroEditado.Descricao;
        }

        public override bool Equals(object? obj)
        {
            return obj is GrupoAutomovel grupo &&
                   Nome.Equals(grupo.Nome, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Nome);
        }
    }
}