using LocadoraDeVeiculos.Core.Dominio.ModuloDevolucao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloDevolucao
{
    public class MapeadorDevolucaoEmOrm : IEntityTypeConfiguration<Devolucao>
    {
        public void Configure(EntityTypeBuilder<Devolucao> builder)
        {
            builder.HasKey(d => d.Id);

            builder.HasOne(d => d.Aluguel)
                   .WithOne()
                   .HasForeignKey<Devolucao>(d => d.AluguelId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(d => d.DataDevolucao)
                   .HasColumnType("datetimeoffset")
                   .IsRequired();

            builder.Property(d => d.QuilometragemFinal)
                   .HasColumnType("decimal(10,2)")
                   .IsRequired();

            builder.Property(d => d.CombustivelNoTanque)
                   .HasColumnType("decimal(5,2)")
                   .IsRequired();

            builder.Property(d => d.NivelCombustivel)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(d => d.ValorMultas)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(d => d.ValorAdicionalCombustivel)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(d => d.ValorTotal)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            // Índices
            builder.HasIndex(d => new { d.EmpresaId, d.Excluido });
            builder.HasIndex(d => d.AluguelId)
                   .IsUnique()
                   .HasFilter("[Excluido] = 0");
        }
    }
}