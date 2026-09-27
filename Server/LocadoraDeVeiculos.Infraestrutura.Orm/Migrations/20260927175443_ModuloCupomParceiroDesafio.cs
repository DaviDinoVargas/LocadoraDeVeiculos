using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Migrations
{
    /// <inheritdoc />
    public partial class ModuloCupomParceiroDesafio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CupomId",
                table: "Alugueis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorDesconto",
                table: "Alugueis",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Parceiros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    Cnpj = table.Column<string>(type: "varchar(18)", nullable: false),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parceiros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parceiros_AspNetUsers_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(30)", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    TipoDesconto = table.Column<int>(type: "int", nullable: false),
                    ValorDesconto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValidoAte = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LimiteUsos = table.Column<int>(type: "int", nullable: true),
                    UsosAtuais = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    ParceiroId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cupons_AspNetUsers_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Cupons_Parceiros_ParceiroId",
                        column: x => x.ParceiroId,
                        principalTable: "Parceiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DesafiosCupom",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    MetaQuantidadeAlugueis = table.Column<int>(type: "int", nullable: false),
                    PeriodoDias = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CupomRecompensaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExcluidoEmUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Excluido = table.Column<bool>(type: "bit", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesafiosCupom", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesafiosCupom_AspNetUsers_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DesafiosCupom_Cupons_CupomRecompensaId",
                        column: x => x.CupomRecompensaId,
                        principalTable: "Cupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alugueis_CupomId",
                table: "Alugueis",
                column: "CupomId");

            migrationBuilder.CreateIndex(
                name: "IX_Cupons_EmpresaId_Codigo",
                table: "Cupons",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cupons_EmpresaId_Excluido",
                table: "Cupons",
                columns: new[] { "EmpresaId", "Excluido" });

            migrationBuilder.CreateIndex(
                name: "IX_Cupons_ParceiroId",
                table: "Cupons",
                column: "ParceiroId");

            migrationBuilder.CreateIndex(
                name: "IX_DesafiosCupom_CupomRecompensaId",
                table: "DesafiosCupom",
                column: "CupomRecompensaId");

            migrationBuilder.CreateIndex(
                name: "IX_DesafiosCupom_EmpresaId_Excluido",
                table: "DesafiosCupom",
                columns: new[] { "EmpresaId", "Excluido" });

            migrationBuilder.CreateIndex(
                name: "IX_Parceiros_EmpresaId_Cnpj",
                table: "Parceiros",
                columns: new[] { "EmpresaId", "Cnpj" },
                unique: true,
                filter: "[Excluido] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Parceiros_EmpresaId_Excluido",
                table: "Parceiros",
                columns: new[] { "EmpresaId", "Excluido" });

            migrationBuilder.AddForeignKey(
                name: "FK_Alugueis_Cupons_CupomId",
                table: "Alugueis",
                column: "CupomId",
                principalTable: "Cupons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Alugueis_Cupons_CupomId",
                table: "Alugueis");

            migrationBuilder.DropTable(
                name: "DesafiosCupom");

            migrationBuilder.DropTable(
                name: "Cupons");

            migrationBuilder.DropTable(
                name: "Parceiros");

            migrationBuilder.DropIndex(
                name: "IX_Alugueis_CupomId",
                table: "Alugueis");

            migrationBuilder.DropColumn(
                name: "CupomId",
                table: "Alugueis");

            migrationBuilder.DropColumn(
                name: "ValorDesconto",
                table: "Alugueis");
        }
    }
}
