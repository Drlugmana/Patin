using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TurneroTcs.Data;

#nullable disable

namespace TurneroTcs.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260306110000_AddModoReemplazoCalamidad")]
public partial class AddModoReemplazoCalamidad : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "modo_reemplazo",
            table: "calamidad_reemplazo",
            type: "varchar(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "SWAP");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "modo_reemplazo",
            table: "calamidad_reemplazo");
    }
}
