using System;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;

/// <summary>
/// Trilha de auditoria (S14): quem fez o quê, quando e de onde. Guarda só nomes (entidade, campos
/// alterados) e identificadores, nunca os valores, para não duplicar dados pessoais nem biometria.
/// Isolada por empresa (tenant).
/// </summary>
public class RegistroDeAuditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? EmpresaId { get; set; }

    /// <summary>Usuário autenticado que provocou a mudança (null em operações sem usuário, como migrações).</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Criado, Alterado, Excluído (exclusão lógica) ou Removido (apagado de verdade).</summary>
    public string Acao { get; set; } = null!;

    public string Entidade { get; set; } = null!;

    public Guid? EntidadeId { get; set; }

    /// <summary>Nomes dos campos alterados, separados por vírgula (só em "Alterado").</summary>
    public string? CamposAlterados { get; set; }

    public string? EnderecoIp { get; set; }

    public DateTime EmUtc { get; set; } = DateTime.UtcNow;
}
