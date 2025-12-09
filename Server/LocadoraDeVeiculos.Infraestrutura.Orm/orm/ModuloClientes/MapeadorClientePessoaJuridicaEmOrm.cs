using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente
{
    public class MapeadorClientePessoaJuridicaEmOrm : IEntityTypeConfiguration<ClientePessoaJuridica>
    {
        public void Configure(EntityTypeBuilder<ClientePessoaJuridica> builder)
        {
            // Propriedades específicas de Pessoa Jurídica
            builder.Property(c => c.Cnpj)
                .HasColumnType("varchar(18)");

            builder.Property(c => c.NomeFantasia)
                .HasColumnType("nvarchar(200)");

            builder.HasIndex(c => c.Cnpj)
                .IsUnique()
                .HasFilter("[TipoCliente] = 2 AND [Excluido] = 0");
        }
    }
}
