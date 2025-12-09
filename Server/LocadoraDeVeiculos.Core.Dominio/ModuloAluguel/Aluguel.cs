using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using System;
using System.Collections.Generic;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAluguel
{
    public class Aluguel : EntidadeBase<Aluguel>
    {
        // Relacionamentos principais
        public Guid CondutorId { get; set; }
        public Condutor? Condutor { get; set; }

        public Guid AutomovelId { get; set; }
        public Automovel? Automovel { get; set; }

        public Guid ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        // Datas
        public DateTimeOffset DataSaida { get; set; }
        public DateTimeOffset DataRetornoPrevisto { get; set; }

        // Valores
        public decimal ValorPrevisto { get; set; }
        public decimal ValorCaucao { get; set; } = 1000m; // Valor fixo conforme requisitos

        // Status
        public StatusAluguel Status { get; set; } = StatusAluguel.Reservado;

        // Taxas e Serviços selecionados
        public List<TaxaServico> TaxasServicos { get; set; } = new();

        protected Aluguel() { }

        public Aluguel(
            Guid condutorId,
            Guid automovelId,
            Guid clienteId,
            DateTimeOffset dataSaida,
            DateTimeOffset dataRetornoPrevisto,
            decimal valorPrevisto)
        {
            CondutorId = condutorId;
            AutomovelId = automovelId;
            ClienteId = clienteId;
            DataSaida = dataSaida;
            DataRetornoPrevisto = dataRetornoPrevisto;
            ValorPrevisto = valorPrevisto;
        }

        public override void AtualizarRegistro(Aluguel registroEditado)
        {
            CondutorId = registroEditado.CondutorId;
            AutomovelId = registroEditado.AutomovelId;
            ClienteId = registroEditado.ClienteId;
            DataSaida = registroEditado.DataSaida;
            DataRetornoPrevisto = registroEditado.DataRetornoPrevisto;
            ValorPrevisto = registroEditado.ValorPrevisto;
            Status = registroEditado.Status;
            TaxasServicos = registroEditado.TaxasServicos;
        }

        public void IniciarAluguel()
        {
            if (Status != StatusAluguel.Reservado)
                throw new InvalidOperationException("Só é possível iniciar um aluguel que esteja Reservado.");

            Status = StatusAluguel.EmAndamento;
        }

        public bool PodeSerEditado()
        {
            return Status != StatusAluguel.Concluido && Status != StatusAluguel.Cancelado;
        }

        public bool PodeSerExcluido()
        {
            return Status != StatusAluguel.Concluido && Status != StatusAluguel.EmAndamento;
        }
    }

    public enum StatusAluguel
    {
        Reservado = 1,      // Criado mas não iniciado
        EmAndamento = 2,    // Veículo retirado
        Concluido = 3,      // Devolvido 
        Cancelado = 4       // Cancelado antes da retirada
    }
}