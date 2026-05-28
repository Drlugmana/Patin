using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TurneroTcs.Data;

#nullable disable

namespace TurneroTcs.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260220213000_RemoveActivoFromPermisoAcceso")]
public partial class RemoveActivoFromPermisoAcceso : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_permiso_acceso_activo",
            table: "permiso_acceso");

        migrationBuilder.DropColumn(
            name: "activo",
            table: "permiso_acceso");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "activo",
            table: "permiso_acceso",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.CreateIndex(
            name: "IX_permiso_acceso_activo",
            table: "permiso_acceso",
            column: "activo");
    }
}
