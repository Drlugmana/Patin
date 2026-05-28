using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanificacionBlueprintAndEquipoTipoGeneracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_generacion",
                table: "equipo",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Rotacion");

            migrationBuilder.CreateTable(
                name: "planificacion_blueprint",
                columns: table => new
                {
                    planificacion_blueprint_id = table.Column<string>(type: "varchar(12)", nullable: false, defaultValueSql: "generate_short_id()"),
                    equipo_id = table.Column<string>(type: "text", nullable: false),
                    grupo_id = table.Column<string>(type: "text", nullable: true),
                    dia = table.Column<string>(type: "varchar(12)", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false),
                    etiquetas = table.Column<string>(type: "varchar(250)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planificacion_blueprint", x => x.planificacion_blueprint_id);
                    table.ForeignKey(
                        name: "FK_planificacion_blueprint_equipo_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "EquipoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planificacion_blueprint_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planificacion_blueprint_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_blueprint_equipo_id",
                table: "planificacion_blueprint",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_blueprint_equipo_id_grupo_id_dia_tipo_turno_id",
                table: "planificacion_blueprint",
                columns: new[] { "equipo_id", "grupo_id", "dia", "tipo_turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_blueprint_grupo_id",
                table: "planificacion_blueprint",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_blueprint_tipo_turno_id",
                table: "planificacion_blueprint",
                column: "tipo_turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planificacion_blueprint");

            migrationBuilder.DropColumn(
                name: "tipo_generacion",
                table: "equipo");
        }
    }
}
