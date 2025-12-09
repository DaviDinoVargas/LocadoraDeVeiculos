using LocadoraDeVeiculos.Core.Dominio.ModuloConfiguracao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloConfiguracao
{
    public class MapeadorConfiguracaoEmOrm : IEntityTypeConfiguration<Configuracao>
    {
        public void Configure(EntityTypeBuilder<Configuracao> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.PrecoCombustivel)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(c => c.ValorGarantia)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.HasIndex(c => c.EmpresaId)
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}