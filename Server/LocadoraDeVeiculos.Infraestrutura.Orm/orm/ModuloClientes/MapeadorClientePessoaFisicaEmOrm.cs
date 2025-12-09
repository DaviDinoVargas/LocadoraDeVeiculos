using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente
{
    public class MapeadorClientePessoaFisicaEmOrm : IEntityTypeConfiguration<ClientePessoaFisica>
    {
        public void Configure(EntityTypeBuilder<ClientePessoaFisica> builder)
        {
            builder.Property(c => c.Cpf)
                .HasColumnType("varchar(14)");

            builder.Property(c => c.Rg)
                .HasColumnType("varchar(20)");

            builder.Property(c => c.Cnh)
                .HasColumnType("varchar(20)");

            builder.Property(c => c.ValidadeCnh)
                .HasColumnType("datetime2");

            builder.HasIndex(c => c.Cpf)
                .IsUnique()
                .HasFilter("[TipoCliente] = 1 AND [Excluido] = 0");

            builder.HasOne(c => c.ClientePessoaJuridica)
                .WithMany()
                .HasForeignKey(c => c.ClientePessoaJuridicaId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
