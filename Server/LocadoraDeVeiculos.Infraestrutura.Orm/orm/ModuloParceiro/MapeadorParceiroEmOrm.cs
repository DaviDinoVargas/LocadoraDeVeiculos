using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloParceiro
{
    public class MapeadorParceiroEmOrm : IEntityTypeConfiguration<Parceiro>
    {
        public void Configure(EntityTypeBuilder<Parceiro> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Nome)
                   .HasColumnType("nvarchar(150)")
                   .IsRequired();

            builder.Property(p => p.Cnpj)
                   .HasColumnType("varchar(18)")
                   .IsRequired();

            builder.Property(p => p.Categoria)
                   .HasConversion<int>()
                   .IsRequired();

            builder.HasIndex(p => new { p.EmpresaId, p.Excluido });
            builder.HasIndex(p => new { p.EmpresaId, p.Cnpj })
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}
