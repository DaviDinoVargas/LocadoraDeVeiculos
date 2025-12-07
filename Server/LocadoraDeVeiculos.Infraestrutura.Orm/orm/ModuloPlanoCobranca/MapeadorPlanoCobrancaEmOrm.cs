using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using LocadoraDeVeiculos.Core.Dominio.ModuloPlanoCobranca;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.ModuloPlanoCobranca
{
    public class MapeadorPlanoCobrancaEmOrm : IEntityTypeConfiguration<PlanoCobranca>
    {
        public void Configure(EntityTypeBuilder<PlanoCobranca> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Nome)
                .HasColumnType("nvarchar(100)")
                .IsRequired();

            builder.Property(p => p.PrecoDiaria)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(p => p.PrecoPorKm)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(p => p.KmLivreLimite)
                .HasColumnType("int")
                .IsRequired();

            builder.HasOne<GrupoAutomovel>()
                .WithMany()
                .HasForeignKey(p => p.GrupoAutomovelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(p => new { p.EmpresaId, p.Excluido });
        }
    }
}
