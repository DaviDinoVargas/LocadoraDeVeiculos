using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloCondutor
{
    public class Condutor : EntidadeBase<Condutor>
    {
        public string Nome { get; set; }
        public string Email { get; set; }
        public string Cpf { get; set; }
        public string Cnh { get; set; }
        public DateTime ValidadeCnh { get; set; }
        public string Telefone { get; set; }

        public Guid? ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        protected Condutor() { }

        public Condutor(
            string nome,
            string email,
            string cpf,
            string cnh,
            DateTime validadeCnh,
            string telefone,
            Guid? clienteId = null)
        {
            Nome = nome;
            Email = email;
            Cpf = cpf;
            Cnh = cnh;
            ValidadeCnh = validadeCnh;
            Telefone = telefone;
            ClienteId = clienteId;
        }

        public override void AtualizarRegistro(Condutor registroEditado)
        {
            Nome = registroEditado.Nome;
            Email = registroEditado.Email;
            Cpf = registroEditado.Cpf;
            Cnh = registroEditado.Cnh;
            ValidadeCnh = registroEditado.ValidadeCnh;
            Telefone = registroEditado.Telefone;
            ClienteId = registroEditado.ClienteId;
        }
    }
}