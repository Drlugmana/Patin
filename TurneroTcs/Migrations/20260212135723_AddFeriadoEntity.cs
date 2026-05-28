using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddFeriadoEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_planificacion_blueprint_equipo_equipo_id",
                table: "planificacion_blueprint");

            migrationBuilder.DropIndex(
                name: "IX_planificacion_blueprint_equipo_id",
                table: "planificacion_blueprint");

            migrationBuilder.DropColumn(
                name: "equipo_id",
                table: "planificacion_blueprint");

            migrationBuilder.CreateTable(
                name: "feriado",
                columns: table => new
                {
                    feriado_id = table.Column<string>(type: "varchar(12)", nullable: false, defaultValueSql: "generate_short_id()"),
                    nombre_feriado = table.Column<string>(type: "varchar(120)", nullable: false),
                    inicio_feriado = table.Column<DateOnly>(type: "date", nullable: false),
                    fin_feriado = table.Column<DateOnly>(type: "date", nullable: false),
                    es_recurrente_anual = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feriado", x => x.feriado_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feriado_es_recurrente_anual",
                table: "feriado",
                column: "es_recurrente_anual");

            migrationBuilder.CreateIndex(
                name: "IX_feriado_fin_feriado",
                table: "feriado",
                column: "fin_feriado");

            migrationBuilder.CreateIndex(
                name: "IX_feriado_inicio_feriado",
                table: "feriado",
                column: "inicio_feriado");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feriado");

            migrationBuilder.AddColumn<string>(
                name: "equipo_id",
                table: "planificacion_blueprint",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_planificacion_blueprint_equipo_id",
                table: "planificacion_blueprint",
                column: "equipo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_planificacion_blueprint_equipo_equipo_id",
                table: "planificacion_blueprint",
                column: "equipo_id",
                principalTable: "equipo",
                principalColumn: "EquipoId");
        }
    }
}
