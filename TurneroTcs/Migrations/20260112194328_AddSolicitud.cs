using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitud",
                columns: table => new
                {
                    solicitud_id = table.Column<string>(type: "text", nullable: false),
                    persona_solicitante_id = table.Column<string>(type: "text", nullable: false),
                    tipo_solicitud_id = table.Column<string>(type: "text", nullable: false),
                    estado_solicitud = table.Column<string>(type: "varchar(20)", nullable: false),
                    fecha_solicitud = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_aprobacion_1 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_aprobacion_2 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    persona_aprobador_1_id = table.Column<string>(type: "text", nullable: true),
                    persona_aprobador_2_id = table.Column<string>(type: "text", nullable: true),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitud", x => x.solicitud_id);
                    table.ForeignKey(
                        name: "FK_solicitud_TipoSolicitudes_tipo_solicitud_id",
                        column: x => x.tipo_solicitud_id,
                        principalTable: "TipoSolicitudes",
                        principalColumn: "tipo_solicitud_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitud_persona_persona_aprobador_1_id",
                        column: x => x.persona_aprobador_1_id,
                        principalTable: "persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitud_persona_persona_aprobador_2_id",
                        column: x => x.persona_aprobador_2_id,
                        principalTable: "persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitud_persona_persona_solicitante_id",
                        column: x => x.persona_solicitante_id,
                        principalTable: "persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_estado_solicitud",
                table: "solicitud",
                column: "estado_solicitud");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_fecha_solicitud",
                table: "solicitud",
                column: "fecha_solicitud");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_persona_aprobador_1_id",
                table: "solicitud",
                column: "persona_aprobador_1_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_persona_aprobador_2_id",
                table: "solicitud",
                column: "persona_aprobador_2_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_persona_solicitante_id",
                table: "solicitud",
                column: "persona_solicitante_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_tipo_solicitud_id",
                table: "solicitud",
                column: "tipo_solicitud_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitud");
        }
    }
}
