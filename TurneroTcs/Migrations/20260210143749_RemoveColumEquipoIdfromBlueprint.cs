using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class RemoveColumEquipoIdfromBlueprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_planificacion_blueprint_equipo_equipo_id",
                table: "planificacion_blueprint");

            migrationBuilder.DropIndex(
                name: "IX_planificacion_blueprint_equipo_id_grupo_id_dia_tipo_turno_id",
                table: "planificacion_blueprint");

            migrationBuilder.AlterColumn<string>(
                name: "equipo_id",
                table: "planificacion_blueprint",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_blueprint_grupo_id_dia_tipo_turno_id",
                table: "planificacion_blueprint",
                columns: new[] { "grupo_id", "dia", "tipo_turno_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_planificacion_blueprint_equipo_equipo_id",
                table: "planificacion_blueprint",
                column: "equipo_id",
                principalTable: "equipo",
                principalColumn: "EquipoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_planificacion_blueprint_equipo_equipo_id",
                table: "planificacion_blueprint");

            migrationBuilder.DropIndex(
                name: "IX_planificacion_blueprint_grupo_id_dia_tipo_turno_id",
                table: "planificacion_blueprint");

            migrationBuilder.AlterColumn<string>(
                name: "equipo_id",
                table: "planificacion_blueprint",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_blueprint_equipo_id_grupo_id_dia_tipo_turno_id",
                table: "planificacion_blueprint",
                columns: new[] { "equipo_id", "grupo_id", "dia", "tipo_turno_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_planificacion_blueprint_equipo_equipo_id",
                table: "planificacion_blueprint",
                column: "equipo_id",
                principalTable: "equipo",
                principalColumn: "EquipoId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
