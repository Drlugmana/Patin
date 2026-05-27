using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class CreateExcepcionTurnoPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "excepcion_turno_persona",
                columns: table => new
                {
                    excepcion_turno_persona_id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    persona_id = table.Column<string>(type: "text", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false),
                    motivo_excepcion = table.Column<string>(type: "varchar(250)", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creado_por = table.Column<string>(type: "varchar(250)", nullable: true),
                    fecha_ultima_actualizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actualizado_por = table.Column<string>(type: "varchar(250)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_excepcion_turno_persona", x => x.excepcion_turno_persona_id);
                    table.ForeignKey(
                        name: "FK_excepcion_turno_persona_persona_persona_id",
                        column: x => x.persona_id,
                        principalTable: "persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_excepcion_turno_persona_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_excepcion_turno_persona_persona_id_fecha_inicio_fecha_fin",
                table: "excepcion_turno_persona",
                columns: new[] { "persona_id", "fecha_inicio", "fecha_fin" });

            migrationBuilder.CreateIndex(
                name: "IX_excepcion_turno_persona_tipo_turno_id",
                table: "excepcion_turno_persona",
                column: "tipo_turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "excepcion_turno_persona");
        }
    }
}
