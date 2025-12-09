using LocadoraDeVeiculos.Core.Dominio.ModuloAluguel;
using LocadoraDeVeiculos.Core.Dominio.ModuloTaxaServico;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloAluguel
{
    public class MapeadorAluguelEmOrm : IEntityTypeConfiguration<Aluguel>
    {
        public void Configure(EntityTypeBuilder<Aluguel> builder)
        {
            builder.HasKey(a => a.Id);

            // Relacionamentos
            builder.HasOne(a => a.Condutor)
                   .WithMany()
                   .HasForeignKey(a => a.CondutorId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.Automovel)
                   .WithMany()
                   .HasForeignKey(a => a.AutomovelId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.Cliente)
                   .WithMany()
                   .HasForeignKey(a => a.ClienteId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Propriedades
            builder.Property(a => a.DataSaida)
                   .HasColumnType("datetimeoffset")
                   .IsRequired();

            builder.Property(a => a.DataRetornoPrevisto)
                   .HasColumnType("datetimeoffset")
                   .IsRequired();

            builder.Property(a => a.ValorPrevisto)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(a => a.ValorCaucao)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(a => a.Status)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(a => a.QuilometragemInicial)
              .HasColumnType("decimal(10,2)")
              .IsRequired(false);

            // Relacionamento Many-to-Many

            builder
                .HasMany(a => a.TaxasServicos)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "AluguelTaxasServicos",
                    j => j.HasOne<TaxaServico>()
                          .WithMany()
                          .HasForeignKey("TaxasServicosId")
                          .OnDelete(DeleteBehavior.Restrict), 
                    j => j.HasOne<Aluguel>()
                          .WithMany()
                          .HasForeignKey("AluguelId")
                          .OnDelete(DeleteBehavior.Cascade)  
                );

            // Índices
            builder.HasIndex(a => new { a.EmpresaId, a.Excluido });
            builder.HasIndex(a => new { a.AutomovelId, a.Status })
                   .HasFilter("[Excluido] = 0");
        }
    }
}
