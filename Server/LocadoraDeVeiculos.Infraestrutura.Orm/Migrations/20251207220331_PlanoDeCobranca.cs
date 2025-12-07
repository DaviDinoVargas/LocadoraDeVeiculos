using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Migrations
{
    /// <inheritdoc />
    public partial class PlanoDeCobranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanoCobranca",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoAutomovelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", nullable: false),
                    PrecoDiaria = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecoPorKm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KmLivreLimite = table.Column<int>(type: "int", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanoCobranca", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanoCobranca_AspNetUsers_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanoCobranca_GruposAutomovel_GrupoAutomovelId",
                        column: x => x.GrupoAutomovelId,
                        principalTable: "GruposAutomovel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanoCobranca_EmpresaId_Excluido",
                table: "PlanoCobranca",
                columns: new[] { "EmpresaId", "Excluido" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanoCobranca_GrupoAutomovelId",
                table: "PlanoCobranca",
                column: "GrupoAutomovelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanoCobranca");
        }
    }
}
