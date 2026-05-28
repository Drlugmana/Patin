using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoTurnoConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grupo_turno_config",
                columns: table => new
                {
                    grupo_turno_config_id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    grupo_id = table.Column<string>(type: "text", nullable: false),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: false),
                    dia = table.Column<string>(type: "varchar(10)", nullable: false),
                    numero_personas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupo_turno_config", x => x.grupo_turno_config_id);
                    table.ForeignKey(
                        name: "FK_grupo_turno_config_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_grupo_turno_config_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grupo_turno_config_grupo_id",
                table: "grupo_turno_config",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "IX_grupo_turno_config_grupo_id_dia_tipo_turno_id",
                table: "grupo_turno_config",
                columns: new[] { "grupo_id", "dia", "tipo_turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grupo_turno_config_tipo_turno_id",
                table: "grupo_turno_config",
                column: "tipo_turno_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grupo_turno_config");
        }
    }
}
