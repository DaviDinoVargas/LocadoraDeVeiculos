using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente
{
    public class MapeadorClienteEmOrm : IEntityTypeConfiguration<Cliente>
    {
        public void Configure(EntityTypeBuilder<Cliente> builder)
        {
            builder.ToTable("Clientes");

            builder.HasKey(c => c.Id);

            builder.HasDiscriminator<TipoCliente>("TipoCliente")
                .HasValue<ClientePessoaFisica>(TipoCliente.PessoaFisica)
                .HasValue<ClientePessoaJuridica>(TipoCliente.PessoaJuridica);

            // Propriedades comuns
            builder.Property(c => c.Nome)
                .HasColumnType("nvarchar(200)")
                .IsRequired();

            builder.Property(c => c.Telefone)
                .HasColumnType("varchar(20)")
                .IsRequired();

            builder.Property(c => c.Email)
                .HasColumnType("varchar(100)")
                .IsRequired();

            builder.Property(c => c.Endereco)
                .HasColumnType("nvarchar(500)")
                .IsRequired();

            builder.HasIndex(c => new { c.EmpresaId, c.Excluido });
        }
    }
}
