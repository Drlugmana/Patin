using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddGrupoEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grupo",
                columns: table => new
                {
                    grupo_id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    nombre_grupo = table.Column<string>(type: "varchar(50)", nullable: false),
                    equipo_id = table.Column<string>(type: "text", nullable: false),
                    color_grupo = table.Column<string>(type: "varchar(11)", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grupo", x => x.grupo_id);
                    table.ForeignKey(
                        name: "FK_grupo_equipo_equipo_id",
                        column: x => x.equipo_id,
                        principalTable: "equipo",
                        principalColumn: "EquipoId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grupo_equipo_id",
                table: "grupo",
                column: "equipo_id");

            migrationBuilder.CreateIndex(
                name: "IX_grupo_equipo_id_nombre_grupo",
                table: "grupo",
                columns: new[] { "equipo_id", "nombre_grupo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grupo");
        }
    }
}
