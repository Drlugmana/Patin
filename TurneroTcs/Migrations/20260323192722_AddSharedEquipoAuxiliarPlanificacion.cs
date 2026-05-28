using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedEquipoAuxiliarPlanificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planificacion_auxiliar_equipo",
                columns: table => new
                {
                    planificacion_auxiliar_equipo_id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    equipo_id = table.Column<string>(type: "text", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false),
                    desde_dia = table.Column<string>(type: "text", nullable: false),
                    hasta_dia = table.Column<string>(type: "text", nullable: false),
                    max_por_dia = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planificacion_auxiliar_equipo", x => x.planificacion_auxiliar_equipo_id);
                    table.ForeignKey(
                        name: "FK_planificacion_auxiliar_equipo_equipo_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "EquipoId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_planificacion_auxiliar_equipo_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planificacion_auxiliar_equipo_grupo",
                columns: table => new
                {
                    planificacion_auxiliar_equipo_grupo_id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    planificacion_auxiliar_equipo_id = table.Column<string>(type: "text", nullable: false),
                    grupo_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planificacion_auxiliar_equipo_grupo", x => x.planificacion_auxiliar_equipo_grupo_id);
                    table.ForeignKey(
                        name: "FK_planificacion_auxiliar_equipo_grupo_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planificacion_auxiliar_equipo_grupo_planificacion_auxiliar_~",
                        column: x => x.planificacion_auxiliar_equipo_id,
                        principalTable: "planificacion_auxiliar_equipo",
                        principalColumn: "planificacion_auxiliar_equipo_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_auxiliar_equipo_equipo_id_tipo_turno_id",
                table: "planificacion_auxiliar_equipo",
                columns: new[] { "equipo_id", "tipo_turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_auxiliar_equipo_tipo_turno_id",
                table: "planificacion_auxiliar_equipo",
                column: "tipo_turno_id");

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_auxiliar_equipo_grupo_grupo_id",
                table: "planificacion_auxiliar_equipo_grupo",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_auxiliar_equipo_grupo_planificacion_auxiliar_~",
                table: "planificacion_auxiliar_equipo_grupo",
                columns: new[] { "planificacion_auxiliar_equipo_id", "grupo_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planificacion_auxiliar_equipo_grupo");

            migrationBuilder.DropTable(
                name: "planificacion_auxiliar_equipo");
        }
    }
}
