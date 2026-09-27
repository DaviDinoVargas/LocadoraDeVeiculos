using LocadoraDeVeiculos.Core.Dominio.ModuloDesafioCupom;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDesafioCupom
{
    public class MapeadorDesafioCupomEmOrm : IEntityTypeConfiguration<DesafioCupom>
    {
        public void Configure(EntityTypeBuilder<DesafioCupom> builder)
        {
            builder.HasKey(d => d.Id);

            builder.Property(d => d.Nome)
                   .HasColumnType("nvarchar(150)")
                   .IsRequired();

            builder.Property(d => d.Descricao)
                   .HasColumnType("nvarchar(300)")
                   .IsRequired();

            builder.HasOne(d => d.CupomRecompensa)
                   .WithMany()
                   .HasForeignKey(d => d.CupomRecompensaId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(d => new { d.EmpresaId, d.Excluido });
        }
    }
}
