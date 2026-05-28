using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddCambioTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cambio_turno",
                columns: table => new
                {
                    cambio_turno_id = table.Column<string>(type: "text", nullable: false),
                    solicitud_id = table.Column<string>(type: "text", nullable: false),
                    turno_origen_id = table.Column<string>(type: "text", nullable: false),
                    turno_destino_id = table.Column<string>(type: "text", nullable: false),
                    motivo = table.Column<string>(type: "varchar(120)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cambio_turno", x => x.cambio_turno_id);
                    table.ForeignKey(
                        name: "FK_cambio_turno_registro_turno_turno_destino_id",
                        column: x => x.turno_destino_id,
                        principalTable: "registro_turno",
                        principalColumn: "turno_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cambio_turno_registro_turno_turno_origen_id",
                        column: x => x.turno_origen_id,
                        principalTable: "registro_turno",
                        principalColumn: "turno_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cambio_turno_solicitud_solicitud_id",
                        column: x => x.solicitud_id,
                        principalTable: "solicitud",
                        principalColumn: "solicitud_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cambio_turno_solicitud_id",
                table: "cambio_turno",
                column: "solicitud_id");

            migrationBuilder.CreateIndex(
                name: "IX_cambio_turno_turno_destino_id",
                table: "cambio_turno",
                column: "turno_destino_id");

            migrationBuilder.CreateIndex(
                name: "IX_cambio_turno_turno_origen_id",
                table: "cambio_turno",
                column: "turno_origen_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cambio_turno");
        }
    }
}
