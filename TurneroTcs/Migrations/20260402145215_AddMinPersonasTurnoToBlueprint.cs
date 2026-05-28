using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddMinPersonasTurnoToBlueprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "min_personas_turno",
                table: "planificacion_blueprint",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "min_personas_turno",
                table: "planificacion_blueprint");
        }
    }
}
