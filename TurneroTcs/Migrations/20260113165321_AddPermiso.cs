using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPermiso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permiso",
                columns: table => new
                {
                    permiso_id = table.Column<string>(type: "text", nullable: false),
                    solicitud_id = table.Column<string>(type: "text", nullable: false),
                    registro_turno_id = table.Column<string>(type: "text", nullable: false),
                    hora_inicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    hora_fin = table.Column<TimeOnly>(type: "time", nullable: false),
                    motivo = table.Column<string>(type: "varchar(120)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permiso", x => x.permiso_id);
                    table.ForeignKey(
                        name: "FK_permiso_registro_turno_registro_turno_id",
                        column: x => x.registro_turno_id,
                        principalTable: "registro_turno",
                        principalColumn: "turno_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permiso_solicitud_solicitud_id",
                        column: x => x.solicitud_id,
                        principalTable: "solicitud",
                        principalColumn: "solicitud_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permiso_hora_fin",
                table: "permiso",
                column: "hora_fin");

            migrationBuilder.CreateIndex(
                name: "IX_permiso_hora_inicio",
                table: "permiso",
                column: "hora_inicio");

            migrationBuilder.CreateIndex(
                name: "IX_permiso_registro_turno_id",
                table: "permiso",
                column: "registro_turno_id");

            migrationBuilder.CreateIndex(
                name: "IX_permiso_solicitud_id",
                table: "permiso",
                column: "solicitud_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "permiso");
        }
    }
}
