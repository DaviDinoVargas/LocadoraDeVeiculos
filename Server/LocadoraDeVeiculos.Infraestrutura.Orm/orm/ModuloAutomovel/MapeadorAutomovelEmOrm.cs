using LocadoraDeVeiculos.Core.Dominio.ModuloAutomovel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutomovel
{
    public class MapeadorAutomovelEmOrm : IEntityTypeConfiguration<Automovel>
    {
        public void Configure(EntityTypeBuilder<Automovel> builder)
        {
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Placa)
                   .HasColumnType("varchar(7)")
                   .IsRequired();

            builder.Property(a => a.Marca)
                   .HasColumnType("nvarchar(50)")
                   .IsRequired();

            builder.Property(a => a.Cor)
                   .HasColumnType("nvarchar(50)")
                   .IsRequired();

            builder.Property(a => a.Modelo)
                   .HasColumnType("nvarchar(100)")
                   .IsRequired();

            builder.Property(a => a.TipoCombustivel)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(a => a.CapacidadeTanque)
                   .HasColumnType("decimal(5,2)")
                   .IsRequired();

            builder.Property(a => a.Ano)
                   .HasColumnType("int")
                   .IsRequired();

            builder.Property(a => a.Foto)
                   .HasColumnType("nvarchar(max)")
                   .IsRequired(false);

            builder.HasOne(a => a.GrupoAutomovel)
                   .WithMany()
                   .HasForeignKey(a => a.GrupoAutomovelId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(a => new { a.EmpresaId, a.Excluido });
            builder.HasIndex(a => new { a.EmpresaId, a.Placa })
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}