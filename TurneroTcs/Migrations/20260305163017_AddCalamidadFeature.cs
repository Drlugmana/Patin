using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddCalamidadFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calamidad",
                columns: table => new
                {
                    calamidad_id = table.Column<string>(type: "text", nullable: false),
                    solicitud_id = table.Column<string>(type: "text", nullable: false),
                    fecha_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin = table.Column<DateOnly>(type: "date", nullable: false),
                    motivo = table.Column<string>(type: "varchar(240)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calamidad", x => x.calamidad_id);
                    table.ForeignKey(
                        name: "FK_calamidad_solicitud_solicitud_id",
                        column: x => x.solicitud_id,
                        principalTable: "solicitud",
                        principalColumn: "solicitud_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "calamidad_reemplazo",
                columns: table => new
                {
                    calamidad_reemplazo_id = table.Column<string>(type: "text", nullable: false),
                    solicitud_id = table.Column<string>(type: "text", nullable: false),
                    turno_ausente_id = table.Column<string>(type: "text", nullable: false),
                    turno_reemplazo_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calamidad_reemplazo", x => x.calamidad_reemplazo_id);
                    table.ForeignKey(
                        name: "FK_calamidad_reemplazo_registro_turno_turno_ausente_id",
                        column: x => x.turno_ausente_id,
                        principalTable: "registro_turno",
                        principalColumn: "turno_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_calamidad_reemplazo_registro_turno_turno_reemplazo_id",
                        column: x => x.turno_reemplazo_id,
                        principalTable: "registro_turno",
                        principalColumn: "turno_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_calamidad_reemplazo_solicitud_solicitud_id",
                        column: x => x.solicitud_id,
                        principalTable: "solicitud",
                        principalColumn: "solicitud_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_fecha_fin",
                table: "calamidad",
                column: "fecha_fin");

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_fecha_inicio",
                table: "calamidad",
                column: "fecha_inicio");

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_solicitud_id",
                table: "calamidad",
                column: "solicitud_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_reemplazo_solicitud_id",
                table: "calamidad_reemplazo",
                column: "solicitud_id");

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_reemplazo_solicitud_id_turno_ausente_id",
                table: "calamidad_reemplazo",
                columns: new[] { "solicitud_id", "turno_ausente_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_reemplazo_solicitud_id_turno_reemplazo_id",
                table: "calamidad_reemplazo",
                columns: new[] { "solicitud_id", "turno_reemplazo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_reemplazo_turno_ausente_id",
                table: "calamidad_reemplazo",
                column: "turno_ausente_id");

            migrationBuilder.CreateIndex(
                name: "IX_calamidad_reemplazo_turno_reemplazo_id",
                table: "calamidad_reemplazo",
                column: "turno_reemplazo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calamidad");

            migrationBuilder.DropTable(
                name: "calamidad_reemplazo");
        }
    }
}
