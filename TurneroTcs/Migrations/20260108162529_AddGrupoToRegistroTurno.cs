using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoToRegistroTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "grupo_id",
                table: "registro_turno",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_registro_turno_grupo_id",
                table: "registro_turno",
                column: "grupo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_registro_turno_grupo_grupo_id",
                table: "registro_turno",
                column: "grupo_id",
                principalTable: "grupo",
                principalColumn: "grupo_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_registro_turno_grupo_grupo_id",
                table: "registro_turno");

            migrationBuilder.DropIndex(
                name: "IX_registro_turno_grupo_id",
                table: "registro_turno");

            migrationBuilder.DropColumn(
                name: "grupo_id",
                table: "registro_turno");
        }
    }
}
