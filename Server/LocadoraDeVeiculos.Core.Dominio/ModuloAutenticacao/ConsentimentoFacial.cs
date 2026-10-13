using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;

/// <summary>
/// Prova do consentimento específico para guardar o rosto (LGPD, art. 11): quando foi dado e qual versão do texto foi aceita.
/// Existe enquanto o rosto existir: revogar o consentimento (apagar o rosto) remove o registro.
/// </summary>
public class ConsentimentoFacial
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mesmo identificador de <see cref="AmostraFacial.PessoaId"/> (ex.: "usuario:email@dominio.com").</summary>
    public string PessoaId { get; set; } = null!;

    /// <summary>Versão do texto de consentimento que a pessoa aceitou (ver ConsentimentoBiometrico.VersaoAtual).</summary>
    public string VersaoDoTexto { get; set; } = null!;

    public DateTime ConsentidoEmUtc { get; set; } = DateTime.UtcNow;
}
