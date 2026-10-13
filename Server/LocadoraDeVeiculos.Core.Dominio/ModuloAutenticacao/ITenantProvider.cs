using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao
{
    public interface ITenantProvider
    {
        Guid? EmpresaId { get; }
        bool EstaNoCargo(string cargo);

        /// <summary>Usuário autenticado da requisição (para a trilha de auditoria).</summary>
        Guid? UsuarioId => null;

        /// <summary>Endereço de origem da requisição (para a trilha de auditoria).</summary>
        string? EnderecoIp => null;
    }
}
