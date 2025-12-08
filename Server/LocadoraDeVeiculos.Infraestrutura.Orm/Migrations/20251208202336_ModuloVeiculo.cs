using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Migrations
{
    /// <inheritdoc />
    public partial class ModuloVeiculo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Automoveis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Placa = table.Column<string>(type: "varchar(7)", nullable: false),
                    Marca = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Cor = table.Column<string>(type: "nvarchar(50)", nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    TipoCombustivel = table.Column<int>(type: "int", nullable: false),
                    CapacidadeTanque = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Foto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GrupoAutomovelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automoveis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Automoveis_AspNetUsers_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Automoveis_GruposAutomovel_GrupoAutomovelId",
                        column: x => x.GrupoAutomovelId,
                        principalTable: "GruposAutomovel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Automoveis_EmpresaId_Excluido",
                table: "Automoveis",
                columns: new[] { "EmpresaId", "Excluido" });

            migrationBuilder.CreateIndex(
                name: "IX_Automoveis_EmpresaId_Placa",
                table: "Automoveis",
                columns: new[] { "EmpresaId", "Placa" },
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Automoveis_GrupoAutomovelId",
                table: "Automoveis",
                column: "GrupoAutomovelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Automoveis");
        }
    }
}
