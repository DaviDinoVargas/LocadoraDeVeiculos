using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocadoraDeVeiculos.Infraestrutura.Orm.Migrations
{
    /// <inheritdoc />
    public partial class ConsentimentoERetencaoFacial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UltimoUsoEmUtc",
                table: "AmostrasFaciais",
                type: "datetime2",
                nullable: false,
                // Quem já tem rosto guardado começa a contar os 12 meses sem uso a partir desta migration
                // (o padrão do tipo seria o ano 1 e a limpeza apagaria tudo na primeira rodada).
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateTable(
                name: "ConsentimentosFaciais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PessoaId = table.Column<string>(type: "nvarchar(320)", nullable: false),
                    VersaoDoTexto = table.Column<string>(type: "varchar(40)", nullable: false),
                    ConsentidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentimentosFaciais", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentimentosFaciais_PessoaId",
                table: "ConsentimentosFaciais",
                column: "PessoaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentimentosFaciais");

            migrationBuilder.DropColumn(
                name: "UltimoUsoEmUtc",
                table: "AmostrasFaciais");
        }
    }
}
