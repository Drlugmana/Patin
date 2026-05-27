using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanificacionAuxiliarTurnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_planificacion_grupo_id_dia_tipo_turno_id";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE planificacion
                ADD COLUMN IF NOT EXISTS is_auxiliar boolean NOT NULL DEFAULT FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_planificacion_grupo_id_dia_tipo_turno_id_is_auxiliar"
                ON planificacion (grupo_id, dia, tipo_turno_id, is_auxiliar);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_planificacion_grupo_id_dia_tipo_turno_id_is_auxiliar";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE planificacion
                DROP COLUMN IF EXISTS is_auxiliar;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_planificacion_grupo_id_dia_tipo_turno_id"
                ON planificacion (grupo_id, dia, tipo_turno_id);
                """);
        }
    }
}
