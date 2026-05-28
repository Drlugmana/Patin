using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddEsTurnoExtra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_turno_extra",
                table: "registro_turno",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_registro_turno_persona_id_fecha_turno_es_turno_extra",
                table: "registro_turno",
                columns: new[] { "persona_id", "fecha_turno", "es_turno_extra" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_registro_turno_persona_id_fecha_turno_es_turno_extra",
                table: "registro_turno");

            migrationBuilder.DropColumn(
                name: "es_turno_extra",
                table: "registro_turno");
        }
    }
}
