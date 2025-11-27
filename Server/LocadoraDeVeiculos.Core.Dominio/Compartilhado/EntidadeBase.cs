using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.Compartilhado
{
    public abstract class EntidadeBase
    {
        public Guid Id { get; set; }

        protected EntidadeBase()
        {
            Id = Guid.NewGuid();
        }

        public Guid UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }
    }
}
