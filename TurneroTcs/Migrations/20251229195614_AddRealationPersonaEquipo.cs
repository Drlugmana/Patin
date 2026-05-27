using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddRealationPersonaEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "equipo_id",
                table: "persona",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_persona_equipo_id",
                table: "persona",
                column: "equipo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_persona_equipo_equipo_id",
                table: "persona",
                column: "equipo_id",
                principalTable: "equipo",
                principalColumn: "EquipoId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_persona_equipo_equipo_id",
                table: "persona");

            migrationBuilder.DropIndex(
                name: "IX_persona_equipo_id",
                table: "persona");

            migrationBuilder.DropColumn(
                name: "equipo_id",
                table: "persona");
        }
    }
}
