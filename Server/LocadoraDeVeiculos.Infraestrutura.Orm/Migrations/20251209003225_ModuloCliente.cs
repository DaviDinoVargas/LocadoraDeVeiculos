using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Migrations
{
    /// <inheritdoc />
    public partial class ModuloCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    Telefone = table.Column<string>(type: "varchar(20)", nullable: false),
                    Email = table.Column<string>(type: "varchar(100)", nullable: false),
                    Endereco = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    TipoCliente = table.Column<int>(type: "int", nullable: false),
                    Cpf = table.Column<string>(type: "varchar(14)", nullable: true),
                    Rg = table.Column<string>(type: "varchar(20)", nullable: true),
                    Cnh = table.Column<string>(type: "varchar(20)", nullable: true),
                    ValidadeCnh = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClientePessoaJuridicaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Cnpj = table.Column<string>(type: "varchar(18)", nullable: true),
                    NomeFantasia = table.Column<string>(type: "nvarchar(200)", nullable: true),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clientes_AspNetUsers_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Clientes_Clientes_ClientePessoaJuridicaId",
                        column: x => x.ClientePessoaJuridicaId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_ClientePessoaJuridicaId",
                table: "Clientes",
                column: "ClientePessoaJuridicaId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_Excluido",
                table: "Clientes",
                columns: new[] { "EmpresaId", "Excluido" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
