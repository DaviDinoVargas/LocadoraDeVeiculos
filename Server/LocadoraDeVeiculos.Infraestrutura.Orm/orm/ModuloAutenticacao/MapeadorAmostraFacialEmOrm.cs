using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;

public class MapeadorAmostraFacialEmOrm : IEntityTypeConfiguration<AmostraFacial>
{
    public void Configure(EntityTypeBuilder<AmostraFacial> builder)
    {
        builder.ToTable("AmostrasFaciais");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PessoaId)
               .HasColumnType("nvarchar(320)")
               .IsRequired();

        builder.Property(a => a.Conteudo)
               .HasColumnType("varbinary(max)")
               .IsRequired();

        builder.Property(a => a.CriadoEmUtc)
               .IsRequired();

        builder.HasIndex(a => a.PessoaId);
    }
}
