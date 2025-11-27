using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao
{
    public interface ITokenProvider
    {
        IAccessToken GerarTokenDeAcesso(Usuario usuario);
    }
}
