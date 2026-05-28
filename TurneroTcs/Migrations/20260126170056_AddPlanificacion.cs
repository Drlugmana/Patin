using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planificacion",
                columns: table => new
                {
                    planificacion_id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    grupo_id = table.Column<string>(type: "text", nullable: false),
                    dia = table.Column<string>(type: "text", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false),
                    numero_personas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planificacion", x => x.planificacion_id);
                    table.ForeignKey(
                        name: "FK_planificacion_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_planificacion_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_grupo_id",
                table: "planificacion",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_grupo_id_dia_tipo_turno_id",
                table: "planificacion",
                columns: new[] { "grupo_id", "dia", "tipo_turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_tipo_turno_id",
                table: "planificacion",
                column: "tipo_turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planificacion");
        }
    }
}
