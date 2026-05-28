using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonaLogicalDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "activo",
                table: "persona",
                newName: "borrado");

            migrationBuilder.Sql("UPDATE persona SET borrado = NOT borrado;");

            migrationBuilder.AddColumn<DateTime>(
                name: "borrado_en",
                table: "persona",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "borrado_por",
                table: "persona",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "borrado_en",
                table: "persona");

            migrationBuilder.DropColumn(
                name: "borrado_por",
                table: "persona");

            migrationBuilder.Sql("UPDATE persona SET borrado = NOT borrado;");

            migrationBuilder.RenameColumn(
                name: "borrado",
                table: "persona",
                newName: "activo");
        }
    }
}
