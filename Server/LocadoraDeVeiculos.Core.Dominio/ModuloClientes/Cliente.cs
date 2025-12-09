using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloCliente
{
    public abstract class Cliente : EntidadeBase<Cliente>
    {
        public string Nome { get; protected set; }
        public string Telefone { get; protected set; }
        public string Email { get; protected set; }
        public string Endereco { get; protected set; }
        public TipoCliente TipoCliente { get; protected set; }

        protected Cliente() { }

        protected Cliente(
            string nome,
            string telefone,
            string email,
            string endereco,
            TipoCliente tipoCliente)
        {
            Nome = nome;
            Telefone = telefone;
            Email = email;
            Endereco = endereco;
            TipoCliente = tipoCliente;
        }

        public abstract override void AtualizarRegistro(Cliente registroEditado);
    }

    public class ClientePessoaFisica : Cliente
    {
        public string Cpf { get; private set; }
        public string Rg { get; private set; }
        public string Cnh { get; private set; }
        public DateTime? ValidadeCnh { get; private set; }
        public Guid? ClientePessoaJuridicaId { get; private set; }
        public ClientePessoaJuridica? ClientePessoaJuridica { get; private set; }

        private ClientePessoaFisica() { }

        public ClientePessoaFisica(
            string nome,
            string telefone,
            string email,
            string endereco,
            string cpf,
            string rg,
            string cnh,
            DateTime? validadeCnh,
            Guid? clientePessoaJuridicaId = null)
            : base(nome, telefone, email, endereco, TipoCliente.PessoaFisica)
        {
            Cpf = cpf;
            Rg = rg;
            Cnh = cnh;
            ValidadeCnh = validadeCnh;
            ClientePessoaJuridicaId = clientePessoaJuridicaId;
        }

        public override void AtualizarRegistro(Cliente registroEditado)
        {
            if (registroEditado is ClientePessoaFisica clientePF)
            {
                Nome = clientePF.Nome;
                Telefone = clientePF.Telefone;
                Email = clientePF.Email;
                Endereco = clientePF.Endereco;
                Cpf = clientePF.Cpf;
                Rg = clientePF.Rg;
                Cnh = clientePF.Cnh;
                ValidadeCnh = clientePF.ValidadeCnh;
                ClientePessoaJuridicaId = clientePF.ClientePessoaJuridicaId;
            }
        }
    }

    public class ClientePessoaJuridica : Cliente
    {
        public string Cnpj { get; private set; }
        public string NomeFantasia { get; private set; }

        private ClientePessoaJuridica() { }

        public ClientePessoaJuridica(
            string nome,
            string telefone,
            string email,
            string endereco,
            string cnpj,
            string nomeFantasia)
            : base(nome, telefone, email, endereco, TipoCliente.PessoaJuridica)
        {
            Cnpj = cnpj;
            NomeFantasia = nomeFantasia;
        }

        public override void AtualizarRegistro(Cliente registroEditado)
        {
            if (registroEditado is ClientePessoaJuridica clientePJ)
            {
                Nome = clientePJ.Nome;
                Telefone = clientePJ.Telefone;
                Email = clientePJ.Email;
                Endereco = clientePJ.Endereco;
                Cnpj = clientePJ.Cnpj;
                NomeFantasia = clientePJ.NomeFantasia;
            }
        }
    }

    public enum TipoCliente
    {
        PessoaFisica = 1,
        PessoaJuridica = 2
    }
}