using LocadoraDeVeiculos.Core.Dominio.ModuloAutenticacao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAutenticacao;

public class MapeadorConsentimentoFacialEmOrm : IEntityTypeConfiguration<ConsentimentoFacial>
{
    public void Configure(EntityTypeBuilder<ConsentimentoFacial> builder)
    {
        builder.ToTable("ConsentimentosFaciais");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.PessoaId).HasColumnType("nvarchar(320)").IsRequired();
        builder.Property(c => c.VersaoDoTexto).HasColumnType("varchar(40)").IsRequired();
        builder.Property(c => c.ConsentidoEmUtc).IsRequired();

        builder.HasIndex(c => c.PessoaId).IsUnique();
    }
}
