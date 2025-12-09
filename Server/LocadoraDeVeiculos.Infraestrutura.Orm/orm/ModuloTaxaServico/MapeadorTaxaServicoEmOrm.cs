using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloTaxaServico
{
    public class MapeadorTaxaServicoEmOrm : IEntityTypeConfiguration<TaxaServico>
    {
        public void Configure(EntityTypeBuilder<TaxaServico> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Nome)
                   .HasColumnType("nvarchar(100)")
                   .IsRequired();

            builder.Property(t => t.Preco)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(t => t.TipoCalculo)
                   .HasConversion<int>()
                   .IsRequired();

            builder.HasIndex(t => new { t.EmpresaId, t.Excluido });
            builder.HasIndex(t => new { t.EmpresaId, t.Nome })
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}