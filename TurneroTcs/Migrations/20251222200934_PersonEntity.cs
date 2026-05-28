using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class PersonEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION generate_short_id()
RETURNS text AS $$
DECLARE
  chars text := '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz';
  bytes bytea := gen_random_bytes(8);
  result text := '';
  i int;
  idx int;
BEGIN
  FOR i IN 1..8 LOOP
    idx := (get_byte(bytes, i-1) % length(chars)) + 1;
    result := result || substr(chars, idx, 1);
  END LOOP;
  RETURN result;
END;
$$ LANGUAGE plpgsql;
");

            migrationBuilder.CreateTable(
                name: "persona",
                columns: table => new
                {
                    PersonaId = table.Column<string>(type: "text", nullable: false, defaultValueSql: "generate_short_id()"),
                    nombre = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    segundo_nombre = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    apellido = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    segundo_apellido = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ultimatix = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    password = table.Column<string>(type: "varchar(255)", nullable: false),
                    es_lider = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persona", x => x.PersonaId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "persona");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS generate_short_id();");
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS pgcrypto;");
        }
    }
}
