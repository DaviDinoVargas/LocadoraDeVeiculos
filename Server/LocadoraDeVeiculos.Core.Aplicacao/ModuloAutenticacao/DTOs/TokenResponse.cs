using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Aplicacao.ModuloAutenticacao.DTOs;

public class TokenResponse : IAccessToken
{
    public required string Chave { get; set; }
    public required DateTime DataExpiracao { get; set; }
    public required UsuarioAutenticadoDto Usuario { get; set; }
}