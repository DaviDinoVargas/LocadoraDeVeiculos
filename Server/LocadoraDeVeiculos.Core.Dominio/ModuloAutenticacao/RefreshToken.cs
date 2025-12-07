using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }

    public string TokenHash { get; set; } = null!;
    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiraEmUtc { get; set; }

    public DateTime? RevogadoEmUtc { get; set; }
    public string? MotivoRevogacao { get; set; }
    public string? SubstituidoPorTokenHash { get; set; }

    public string? IpCriacao { get; set; }
    public string? UserAgent { get; set; }
}