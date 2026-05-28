using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistroTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registro_turno",
                columns: table => new
                {
                    turno_id = table.Column<string>(type: "text", nullable: false),
                    persona_id = table.Column<string>(type: "text", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false),
                    fecha_turno = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registro_turno", x => x.turno_id);
                    table.ForeignKey(
                        name: "FK_registro_turno_persona_persona_id",
                        column: x => x.persona_id,
                        principalTable: "persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registro_turno_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registro_turno_persona_id",
                table: "registro_turno",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_registro_turno_persona_id_tipo_turno_id",
                table: "registro_turno",
                columns: new[] { "persona_id", "tipo_turno_id" });

            migrationBuilder.CreateIndex(
                name: "IX_registro_turno_tipo_turno_id",
                table: "registro_turno",
                column: "tipo_turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registro_turno");
        }
    }
}
