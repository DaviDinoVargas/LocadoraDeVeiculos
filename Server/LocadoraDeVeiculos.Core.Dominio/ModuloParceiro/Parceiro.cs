using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloParceiro
{
    public class Parceiro : EntidadeBase<Parceiro>
    {
        public string Nome { get; set; }
        public string Cnpj { get; set; }
        public CategoriaParceiro Categoria { get; set; }
        public bool Ativo { get; set; } = true;

        protected Parceiro() { }

        public Parceiro(string nome, string cnpj, CategoriaParceiro categoria)
        {
            Nome = nome;
            Cnpj = cnpj;
            Categoria = categoria;
        }

        public override void AtualizarRegistro(Parceiro registroEditado)
        {
            Nome = registroEditado.Nome;
            Cnpj = registroEditado.Cnpj;
            Categoria = registroEditado.Categoria;
            Ativo = registroEditado.Ativo;
        }

        public void Desativar() => Ativo = false;

        public void Ativar() => Ativo = true;
    }

    public enum CategoriaParceiro
    {
        PostoDeCombustivel = 1,
        Hotel = 2,
        Seguradora = 3,
        Restaurante = 4,
        Oficina = 5,
        Outro = 6
    }
}
