using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class RemovePasswordFromPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password",
                table: "persona");

            migrationBuilder.AddColumn<string>(
                name: "user_id",
                table: "persona",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE persona
SET user_id = u.""Id""
FROM ""AspNetUsers"" u
WHERE u.""UserName"" = persona.ultimatix
  AND (persona.user_id IS NULL OR persona.user_id = '');
");

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                table: "persona",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_persona_user_id",
                table: "persona",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_persona_AspNetUsers_user_id",
                table: "persona",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_persona_AspNetUsers_user_id",
                table: "persona");

            migrationBuilder.DropIndex(
                name: "IX_persona_user_id",
                table: "persona");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "persona");

            migrationBuilder.AddColumn<string>(
                name: "password",
                table: "persona",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "");
        }
    }
}
