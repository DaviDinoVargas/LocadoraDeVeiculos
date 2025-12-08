using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel
{
    public class Automovel : EntidadeBase<Automovel>
    {
        public string Placa { get; set; }
        public string Marca { get; set; }
        public string Cor { get; set; }
        public string Modelo { get; set; }
        public TipoCombustivel TipoCombustivel { get; set; }
        public decimal CapacidadeTanque { get; set; } 
        public int Ano { get; set; }
        public string? Foto { get; set; } // caminho da imagem

        public Guid GrupoAutomovelId { get; set; }
        public GrupoAutomovel? GrupoAutomovel { get; set; }

        protected Automovel() { }

        public Automovel(
            string placa,
            string marca,
            string cor,
            string modelo,
            TipoCombustivel tipoCombustivel,
            decimal capacidadeTanque,
            int ano,
            string? foto,
            Guid grupoAutomovelId)
        {
            Placa = placa;
            Marca = marca;
            Cor = cor;
            Modelo = modelo;
            TipoCombustivel = tipoCombustivel;
            CapacidadeTanque = capacidadeTanque;
            Ano = ano;
            Foto = foto;
            GrupoAutomovelId = grupoAutomovelId;
        }

        public override void AtualizarRegistro(Automovel registroEditado)
        {
            Placa = registroEditado.Placa;
            Marca = registroEditado.Marca;
            Cor = registroEditado.Cor;
            Modelo = registroEditado.Modelo;
            TipoCombustivel = registroEditado.TipoCombustivel;
            CapacidadeTanque = registroEditado.CapacidadeTanque;
            Ano = registroEditado.Ano;
            Foto = registroEditado.Foto;
            GrupoAutomovelId = registroEditado.GrupoAutomovelId;
        }
    }
}