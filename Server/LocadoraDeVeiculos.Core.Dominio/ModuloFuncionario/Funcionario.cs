using LocadoraDeVeiculos.Core.Dominio.Compartilhado;
using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LocadoraDeVeiculos.Core.Dominio.ModuloFuncionario;

public class Funcionario : EntidadeBase<Funcionario>
{
    public string NomeCompleto { get; set; }
    public string Cpf { get; set; }
    public string Email { get; set; }
    public decimal Salario { get; set; }
    public DateTimeOffset AdmissaoEmUtc { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    protected Funcionario() { }

    public Funcionario(
        Guid usuarioId,
        Guid tenantId,
        string nomeCompleto,
        string cpf,
        string email,
        decimal salario,
        DateTimeOffset admissaoEmUtc
    )
    {
        UsuarioId = usuarioId;
        EmpresaId = tenantId;
        NomeCompleto = nomeCompleto;
        Cpf = cpf;
        Email = email;
        Salario = salario;
        AdmissaoEmUtc = admissaoEmUtc;
    }

    public override void AtualizarRegistro(Funcionario registroEditado)
    {
        NomeCompleto = registroEditado.NomeCompleto;
        Cpf = registroEditado.Cpf;
        Salario = registroEditado.Salario;
        AdmissaoEmUtc = registroEditado.AdmissaoEmUtc;
    }
}
