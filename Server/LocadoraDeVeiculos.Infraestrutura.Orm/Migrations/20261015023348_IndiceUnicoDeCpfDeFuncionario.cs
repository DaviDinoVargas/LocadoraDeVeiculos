using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Migrations
{
    /// <inheritdoc />
    public partial class IndiceUnicoDeCpfDeFuncionario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Funcionarios_EmpresaId_Cpf",
                table: "Funcionarios",
                columns: new[] { "EmpresaId", "Cpf" },
                unique: true,
                filter: "[Excluido] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Funcionarios_EmpresaId_Cpf",
                table: "Funcionarios");
        }
    }
}
