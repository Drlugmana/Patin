using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TurneroTcs.Data;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260212193000_AddFeriadoBalancerConfig")]
    public partial class AddFeriadoBalancerConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "es_feriado",
                table: "registro_turno",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "no_laborado_por_feriado",
                table: "registro_turno",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "feriado_cobertura_config",
                columns: table => new
                {
                    feriado_cobertura_config_id = table.Column<string>(type: "varchar(12)", nullable: false, defaultValueSql: "generate_short_id()"),
                    equipo_id = table.Column<string>(type: "text", nullable: false),
                    grupo_id = table.Column<string>(type: "text", nullable: true),
                    tipo_turno_id = table.Column<string>(type: "text", nullable: true),
                    cantidad_visible = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feriado_cobertura_config", x => x.feriado_cobertura_config_id);
                    table.ForeignKey(
                        name: "FK_feriado_cobertura_config_equipo_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "EquipoId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feriado_cobertura_config_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_feriado_cobertura_config_tipo_turno_tipo_turno_id",
                        column: x => x.tipo_turno_id,
                        principalTable: "tipo_turno",
                        principalColumn: "tipo_turno_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feriado_cobertura_config_equipo_id",
                table: "feriado_cobertura_config",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_feriado_cobertura_config_equipo_id_grupo_id",
                table: "feriado_cobertura_config",
                columns: new[] { "equipo_id", "grupo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feriado_cobertura_config_equipo_id_tipo_turno_id",
                table: "feriado_cobertura_config",
                columns: new[] { "equipo_id", "tipo_turno_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feriado_cobertura_config_grupo_id",
                table: "feriado_cobertura_config",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "IX_feriado_cobertura_config_tipo_turno_id",
                table: "feriado_cobertura_config",
                column: "tipo_turno_id");

            migrationBuilder.CreateIndex(
                name: "IX_registro_turno_fecha_turno_es_feriado_no_laborado_por_feriado",
                table: "registro_turno",
                columns: new[] { "fecha_turno", "es_feriado", "no_laborado_por_feriado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feriado_cobertura_config");

            migrationBuilder.DropIndex(
                name: "IX_registro_turno_fecha_turno_es_feriado_no_laborado_por_feriado",
                table: "registro_turno");

            migrationBuilder.DropColumn(
                name: "es_feriado",
                table: "registro_turno");

            migrationBuilder.DropColumn(
                name: "no_laborado_por_feriado",
                table: "registro_turno");
        }
    }
}
