using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanificacionApoyoGrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planificacion_apoyo_grupo",
                columns: table => new
                {
                    planificacion_apoyo_grupo_id = table.Column<string>(type: "varchar(12)", nullable: false, defaultValueSql: "generate_short_id()"),
                    grupo_id = table.Column<string>(type: "text", nullable: false),
                    dia = table.Column<string>(type: "text", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false),
                    cantidad_apoyo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planificacion_apoyo_grupo", x => x.planificacion_apoyo_grupo_id);
                    table.ForeignKey(
                        name: "FK_planificacion_apoyo_grupo_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_planificacion_apoyo_grupo_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_apoyo_grupo_grupo_id_dia_tipo_turno_id",
                table: "planificacion_apoyo_grupo",
                columns: new[] { "grupo_id", "dia", "tipo_turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_apoyo_grupo_tipo_turno_id",
                table: "planificacion_apoyo_grupo",
                column: "tipo_turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planificacion_apoyo_grupo");
        }
    }
}
