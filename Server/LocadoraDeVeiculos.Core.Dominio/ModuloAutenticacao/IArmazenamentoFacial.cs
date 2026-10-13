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

    /// <summary>Apaga de verdade (não é exclusão lógica). Devolve false se não havia nada.</summary>
    Task<bool> RemoverAsync(string pessoaId, CancellationToken cancellationToken);
}
