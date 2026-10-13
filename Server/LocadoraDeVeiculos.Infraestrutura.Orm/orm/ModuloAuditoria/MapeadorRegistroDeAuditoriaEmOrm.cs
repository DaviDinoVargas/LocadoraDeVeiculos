using LocadoraDeVeiculos.Core.Dominio.ModuloAuditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAuditoria;

public class MapeadorRegistroDeAuditoriaEmOrm : IEntityTypeConfiguration<RegistroDeAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroDeAuditoria> builder)
    {
        builder.ToTable("Auditoria");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Acao).HasColumnType("varchar(20)").IsRequired();
        builder.Property(r => r.Entidade).HasColumnType("varchar(80)").IsRequired();
        builder.Property(r => r.CamposAlterados).HasColumnType("nvarchar(1000)");
        builder.Property(r => r.EnderecoIp).HasColumnType("varchar(45)");
        builder.Property(r => r.EmUtc).IsRequired();

        builder.HasIndex(r => new { r.EmpresaId, r.EmUtc });
    }
}
