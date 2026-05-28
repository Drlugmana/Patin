using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanificacionTurnoOpcionalVacacionGrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planificacion_turno_opcional_vacacion_grupo",
                columns: table => new
                {
                    planificacion_turno_opcional_vacacion_grupo_id = table.Column<string>(type: "varchar(12)", nullable: false, defaultValueSql: "generate_short_id()"),
                    grupo_id = table.Column<string>(type: "text", nullable: false),
                    dia = table.Column<string>(type: "text", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planificacion_turno_opcional_vacacion_grupo", x => x.planificacion_turno_opcional_vacacion_grupo_id);
                    table.ForeignKey(
                        name: "FK_planificacion_turno_opcional_vacacion_grupo_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_planificacion_turno_opcional_vacacion_grupo_tipo_turno_tipo~",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_turno_opcional_vacacion_grupo_grupo_id_dia_ti~",
                table: "planificacion_turno_opcional_vacacion_grupo",
                columns: new[] { "grupo_id", "dia", "tipo_turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_turno_opcional_vacacion_grupo_tipo_turno_id",
                table: "planificacion_turno_opcional_vacacion_grupo",
                column: "tipo_turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planificacion_turno_opcional_vacacion_grupo");
        }
    }
}
