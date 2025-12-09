using LocadoraDeVeiculos.Core.Dominio.ModuloCondutor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCondutor
{
    public class MapeadorCondutorEmOrm : IEntityTypeConfiguration<Condutor>
    {
        public void Configure(EntityTypeBuilder<Condutor> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Nome)
                   .HasColumnType("nvarchar(200)")
                   .IsRequired();

            builder.Property(c => c.Email)
                   .HasColumnType("varchar(100)")
                   .IsRequired();

            builder.Property(c => c.Cpf)
                   .HasColumnType("varchar(14)")
                   .IsRequired();

            builder.Property(c => c.Cnh)
                   .HasColumnType("varchar(20)")
                   .IsRequired();

            builder.Property(c => c.ValidadeCnh)
                   .HasColumnType("datetime2")
                   .IsRequired();

            builder.Property(c => c.Telefone)
                   .HasColumnType("varchar(20)")
                   .IsRequired();

            builder.HasOne(c => c.Cliente)
                   .WithMany()
                   .HasForeignKey(c => c.ClienteId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(c => new { c.EmpresaId, c.Excluido });
            builder.HasIndex(c => new { c.EmpresaId, c.Cpf })
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
            builder.HasIndex(c => new { c.EmpresaId, c.Cnh })
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}