using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.Compartilhado
{
    public abstract class EntidadeBase<T>
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTimeOffset CriadoEmUtc { get; set; } = DateTime.UtcNow;
        public DateTimeOffset? ExcluidoEmUtc { get; set; }
        public bool Excluido { get; set; }

        public Guid EmpresaId { get; set; }
        public Usuario? Empresa { get; set; }

        public abstract void AtualizarRegistro(T registroEditado);

        public void Excluir()
        {
            Excluido = true;
            ExcluidoEmUtc = DateTime.UtcNow;
        }
    }
}
