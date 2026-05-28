using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipoPlanificacionConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipo_planificacion_config",
                columns: table => new
                {
                    equipo_planificacion_config_id = table.Column<string>(type: "varchar(12)", nullable: false, defaultValueSql: "generate_short_id()"),
                    equipo_id = table.Column<string>(type: "text", nullable: false),
                    maximo_slots_fin_semana_por_mes = table.Column<int>(type: "integer", nullable: true),
                    maximo_turnos_nocturnos_por_mes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipo_planificacion_config", x => x.equipo_planificacion_config_id);
                    table.ForeignKey(
                        name: "FK_equipo_planificacion_config_equipo_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "EquipoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_equipo_planificacion_config_equipo_id",
                table: "equipo_planificacion_config",
                column: "equipo_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipo_planificacion_config");
        }
    }
}
