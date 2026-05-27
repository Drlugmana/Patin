using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddVacacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vacacion",
                columns: table => new
                {
                    vacacion_id = table.Column<string>(type: "text", nullable: false),
                    solicitud_id = table.Column<string>(type: "text", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vacacion", x => x.vacacion_id);
                    table.ForeignKey(
                        name: "FK_vacacion_solicitud_solicitud_id",
                        column: x => x.solicitud_id,
                        principalTable: "solicitud",
                        principalColumn: "solicitud_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_fecha_fin",
                table: "vacacion",
                column: "fecha_fin");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_fecha_inicio",
                table: "vacacion",
                column: "fecha_inicio");

            migrationBuilder.CreateIndex(
                name: "IX_vacacion_solicitud_id",
                table: "vacacion",
                column: "solicitud_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vacacion");
        }
    }
}
