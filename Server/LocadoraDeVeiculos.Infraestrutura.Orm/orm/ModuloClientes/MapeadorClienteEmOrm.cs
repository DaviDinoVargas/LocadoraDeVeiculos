using LocadoraDeVeiculos.Core.Dominio.ModuloCliente;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LocadoraDeVeiculos.Infraestrutura.Orm.orm.ModuloCliente
{
    public class MapeadorClienteEmOrm : IEntityTypeConfiguration<Cliente>
    {
        public void Configure(EntityTypeBuilder<Cliente> builder)
        {
            builder.HasKey(c => c.Id);

            // Configuração TPH por conta do tipo de cliente da forma que fiz no dominio
            builder.HasDiscriminator<TipoCliente>("TipoCliente")
                   .HasValue<ClientePessoaFisica>(TipoCliente.PessoaFisica)
                   .HasValue<ClientePessoaJuridica>(TipoCliente.PessoaJuridica);

            // Propriedades comuns
            builder.Property(c => c.Nome)
                   .HasColumnType("nvarchar(200)")
                   .IsRequired();

            builder.Property(c => c.Telefone)
                   .HasColumnType("varchar(20)")
                   .IsRequired();

            builder.Property(c => c.Email)
                   .HasColumnType("varchar(100)")
                   .IsRequired();

            builder.Property(c => c.Endereco)
                   .HasColumnType("nvarchar(500)")
                   .IsRequired();

            // Propriedades específicas de Pessoa Física
            builder.Property("Cpf")
                   .HasColumnType("varchar(14)");

            builder.Property("Rg")
                   .HasColumnType("varchar(20)");

            builder.Property("Cnh")
                   .HasColumnType("varchar(20)");

            builder.Property("ValidadeCnh")
                   .HasColumnType("datetimeoffset");

            // Propriedades específicas de Pessoa Jurídica
            builder.Property("Cnpj")
                   .HasColumnType("varchar(18)");

            builder.Property("NomeFantasia")
                   .HasColumnType("nvarchar(200)");

            // Relacionamento PF -> PJ
            builder.HasOne(typeof(ClientePessoaJuridica), "ClientePessoaJuridica")
                   .WithMany()
                   .HasForeignKey("ClientePessoaJuridicaId")
                   .OnDelete(DeleteBehavior.Restrict);

            // Índices
            builder.HasIndex(c => new { c.EmpresaId, c.Excluido });
            builder.HasIndex("Cpf")
                   .IsUnique()
                   .HasFilter("[TipoCliente] = 1 AND [Excluido] = 0");
            builder.HasIndex("Cnpj")
                   .IsUnique()
                   .HasFilter("[TipoCliente] = 2 AND [Excluido] = 0");
        }
    }
}