using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TurneroTcs.Data;

#nullable disable

namespace TurneroTcs.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260213101500_UpdateFeriadoCoberturaPerGrupoTurno")]
    public partial class UpdateFeriadoCoberturaPerGrupoTurno : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_feriado_cobertura_config_equipo_id_grupo_id",
                table: "feriado_cobertura_config");

            migrationBuilder.DropIndex(
                name: "IX_feriado_cobertura_config_equipo_id_tipo_turno_id",
                table: "feriado_cobertura_config");

            migrationBuilder.Sql("DELETE FROM feriado_cobertura_config WHERE grupo_id IS NULL OR tipo_turno_id IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "tipo_turno_id",
                table: "feriado_cobertura_config",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "grupo_id",
                table: "feriado_cobertura_config",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_feriado_cobertura_config_equipo_id_grupo_id",
                table: "feriado_cobertura_config",
                columns: new[] { "equipo_id", "grupo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_feriado_cobertura_config_equipo_id_grupo_id_tipo_turno_id",
                table: "feriado_cobertura_config",
                columns: new[] { "equipo_id", "grupo_id", "tipo_turno_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_feriado_cobertura_config_equipo_id_grupo_id_tipo_turno_id",
                table: "feriado_cobertura_config");

            migrationBuilder.DropIndex(
                name: "IX_feriado_cobertura_config_equipo_id_grupo_id",
                table: "feriado_cobertura_config");

            migrationBuilder.AlterColumn<string>(
                name: "tipo_turno_id",
                table: "feriado_cobertura_config",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "grupo_id",
                table: "feriado_cobertura_config",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

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
        }
    }
}
