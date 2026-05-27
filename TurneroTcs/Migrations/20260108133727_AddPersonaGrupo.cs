using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonaGrupo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "persona_grupo",
                columns: table => new
                {
                    persona_grupo_id = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    persona_id = table.Column<string>(type: "text", nullable: false),
                    grupo_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persona_grupo", x => x.persona_grupo_id);
                    table.ForeignKey(
                        name: "FK_persona_grupo_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "grupo_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_persona_grupo_persona_persona_id",
                        column: x => x.persona_id,
                        principalTable: "persona",
                        principalColumn: "PersonaId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_persona_grupo_grupo_id",
                table: "persona_grupo",
                column: "grupo_id");

            migrationBuilder.CreateIndex(
                name: "IX_persona_grupo_persona_id_grupo_id",
                table: "persona_grupo",
                columns: new[] { "persona_id", "grupo_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "persona_grupo");
        }
    }
}
