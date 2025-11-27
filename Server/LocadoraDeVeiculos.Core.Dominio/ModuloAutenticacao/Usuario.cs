using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao
{
    public class Usuario : IdentityUser<Guid>
    {
        public Usuario()
        {
            Id = Guid.NewGuid();
            EmailConfirmed = true;
        }
    }
}
