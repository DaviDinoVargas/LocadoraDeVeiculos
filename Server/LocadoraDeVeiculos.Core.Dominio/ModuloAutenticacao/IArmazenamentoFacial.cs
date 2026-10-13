using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;

/// <summary>
/// Guarda as amostras faciais de cada pessoa. O serviço de ML não guarda nada: recebe a imagem (e,
/// na verificação, as amostras) a cada chamada e devolve só o resultado.
/// </summary>
public interface IArmazenamentoFacial
{
    /// <summary>Acrescenta uma amostra, mantendo só as mais recentes. Devolve quantas ficaram guardadas.</summary>
    Task<int> SalvarAsync(string pessoaId, IReadOnlyList<double> landmarks, CancellationToken cancellationToken);

    Task<IReadOnlyList<IReadOnlyList<double>>> ObterAsync(string pessoaId, CancellationToken cancellationToken);

    Task<bool> ExisteAsync(string pessoaId, CancellationToken cancellationToken);

    /// <summary>Apaga de verdade (não é exclusão lógica), junto com o consentimento. Devolve false se não havia nada.</summary>
    Task<bool> RemoverAsync(string pessoaId, CancellationToken cancellationToken);

    /// <summary>Guarda (ou renova) a prova de que a pessoa consentiu, com a versão do texto aceito.</summary>
    Task RegistrarConsentimentoAsync(string pessoaId, string versaoDoTexto, CancellationToken cancellationToken);

    /// <summary>Versão do texto aceito, ou null se a pessoa não consentiu.</summary>
    Task<string?> ObterVersaoDoConsentimentoAsync(string pessoaId, CancellationToken cancellationToken);

    /// <summary>Marca que o rosto foi usado agora (login ou verificação bem-sucedidos): zera a contagem de retenção.</summary>
    Task RegistrarUsoAsync(string pessoaId, CancellationToken cancellationToken);
}
