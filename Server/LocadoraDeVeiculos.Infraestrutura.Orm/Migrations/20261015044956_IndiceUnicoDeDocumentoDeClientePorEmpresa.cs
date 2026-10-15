using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Migrations
{
    /// <inheritdoc />
    public partial class IndiceUnicoDeDocumentoDeClientePorEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clientes_Cnpj",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_Cpf",
                table: "Clientes");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Cnpj",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Cnpj" },
                unique: true,
                filter: "[TipoCliente] = 2 AND [Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Cpf",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Cpf" },
                unique: true,
                filter: "[TipoCliente] = 1 AND [Excluido] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clientes_EmpresaId_Cnpj",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_EmpresaId_Cpf",
                table: "Clientes");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Cnpj",
                table: "Clientes",
                column: "Cnpj",
                unique: true,
                filter: "[TipoCliente] = 2 AND [Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Cpf",
                table: "Clientes",
                column: "Cpf",
                unique: true,
                filter: "[TipoCliente] = 1 AND [Excluido] = 0");
        }
    }
}
