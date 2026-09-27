using LocadoraDeVeiculos.Core.Dominio.ModuloCupom;
using LocadoraDeVeiculos.Core.Dominio.ModuloParceiro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCupom
{
    public class MapeadorCupomEmOrm : IEntityTypeConfiguration<Cupom>
    {
        public void Configure(EntityTypeBuilder<Cupom> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Codigo)
                   .HasColumnType("varchar(30)")
                   .IsRequired();

            builder.Property(c => c.Descricao)
                   .HasColumnType("nvarchar(300)")
                   .IsRequired();

            builder.Property(c => c.TipoDesconto)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(c => c.ValorDesconto)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(c => c.ValidoAte)
                   .HasColumnType("datetimeoffset")
                   .IsRequired();

            builder.HasOne(c => c.Parceiro)
                   .WithMany()
                   .HasForeignKey(c => c.ParceiroId)
                   .OnDelete(DeleteBehavior.Restrict)
                   .IsRequired(false);

            builder.HasIndex(c => new { c.EmpresaId, c.Excluido });
            builder.HasIndex(c => new { c.EmpresaId, c.Codigo })
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}
