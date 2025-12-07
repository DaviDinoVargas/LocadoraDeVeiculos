using LocadoraDeVeiculos.Core.Dominio.ModuloGrupoAutomovel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloGrupoAutomovel
{
    public class MapeadorGrupoAutomovelEmOrm : IEntityTypeConfiguration<GrupoAutomovel>
    {
        public void Configure(EntityTypeBuilder<GrupoAutomovel> builder)
        {
            builder.HasKey(g => g.Id);

            builder.Property(g => g.Nome)
                   .HasColumnType("nvarchar(100)")
                   .IsRequired();

            builder.Property(g => g.Descricao)
                   .HasColumnType("nvarchar(500)")
                   .IsRequired(false);

            builder.HasIndex(g => new { g.EmpresaId, g.Excluido });

            builder.HasIndex(g => new { g.EmpresaId, g.Nome })
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}