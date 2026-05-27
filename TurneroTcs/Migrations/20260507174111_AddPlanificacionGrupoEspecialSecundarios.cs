using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanificacionGrupoEspecialSecundarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grupo_fuente_secundarios_id",
                table: "planificacion",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "usa_solo_secundarios",
                table: "planificacion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "usar_persona_unica_por_semana",
                table: "planificacion",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_grupo_fuente_secundarios_id",
                table: "planificacion",
                column: "grupo_fuente_secundarios_id");

            migrationBuilder.AddForeignKey(
                name: "FK_planificacion_grupo_grupo_fuente_secundarios_id",
                table: "planificacion",
                column: "grupo_fuente_secundarios_id",
                principalTable: "grupo",
                principalColumn: "grupo_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_planificacion_grupo_grupo_fuente_secundarios_id",
                table: "planificacion");

            migrationBuilder.DropIndex(
                name: "IX_planificacion_grupo_fuente_secundarios_id",
                table: "planificacion");

            migrationBuilder.DropColumn(
                name: "grupo_fuente_secundarios_id",
                table: "planificacion");

            migrationBuilder.DropColumn(
                name: "usa_solo_secundarios",
                table: "planificacion");

            migrationBuilder.DropColumn(
                name: "usar_persona_unica_por_semana",
                table: "planificacion");
        }
    }
}
