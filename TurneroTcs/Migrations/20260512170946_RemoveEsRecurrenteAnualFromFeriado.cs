using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEsRecurrenteAnualFromFeriado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_feriado_es_recurrente_anual",
                table: "feriado");

            migrationBuilder.DropColumn(
                name: "es_recurrente_anual",
                table: "feriado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_recurrente_anual",
                table: "feriado",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_feriado_es_recurrente_anual",
                table: "feriado",
                column: "es_recurrente_anual");
        }
    }
}
