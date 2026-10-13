using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;

/// <summary>
/// Uma amostra de landmarks faciais (dado biométrico). Nunca guarda a foto, e o conteúdo fica
/// cifrado em repouso: o banco só enxerga bytes opacos.
/// </summary>
public class AmostraFacial
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identificador da pessoa no módulo facial (ex.: "usuario:email@dominio.com", em minúsculas).</summary>
    public string PessoaId { get; set; } = null!;

    /// <summary>Landmarks normalizados, cifrados com AES-GCM (ver CifradorFacial).</summary>
    public byte[] Conteudo { get; set; } = null!;

    public DateTime CriadoEmUtc { get; set; } = DateTime.UtcNow;
}
