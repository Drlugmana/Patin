using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TurneroTcs.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessPermissionCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permiso_acceso",
                columns: table => new
                {
                    permiso_acceso_id = table.Column<string>(type: "varchar(12)", nullable: false, defaultValueSql: "generate_short_id()"),
                    codigo_permiso = table.Column<string>(type: "varchar(120)", nullable: false),
                    nombre_permiso = table.Column<string>(type: "varchar(120)", nullable: false),
                    descripcion = table.Column<string>(type: "varchar(300)", nullable: true),
                    modulo = table.Column<string>(type: "varchar(80)", nullable: false),
                    es_sistema = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permiso_acceso", x => x.permiso_acceso_id);
                });

            migrationBuilder.CreateTable(
                name: "rol_permiso_acceso",
                columns: table => new
                {
                    role_id = table.Column<string>(type: "text", nullable: false),
                    permiso_acceso_id = table.Column<string>(type: "varchar(12)", nullable: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rol_permiso_acceso", x => new { x.role_id, x.permiso_acceso_id });
                    table.ForeignKey(
                        name: "FK_rol_permiso_acceso_AspNetRoles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_rol_permiso_acceso_permiso_acceso_permiso_acceso_id",
                        column: x => x.permiso_acceso_id,
                        principalTable: "permiso_acceso",
                        principalColumn: "permiso_acceso_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_permiso_acceso",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    permiso_acceso_id = table.Column<string>(type: "varchar(12)", nullable: false),
                    es_denegado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_permiso_acceso", x => new { x.user_id, x.permiso_acceso_id });
                    table.ForeignKey(
                        name: "FK_usuario_permiso_acceso_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_usuario_permiso_acceso_permiso_acceso_permiso_acceso_id",
                        column: x => x.permiso_acceso_id,
                        principalTable: "permiso_acceso",
                        principalColumn: "permiso_acceso_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_permiso_acceso_activo",
                table: "permiso_acceso",
                column: "activo");

            migrationBuilder.CreateIndex(
                name: "IX_permiso_acceso_codigo_permiso",
                table: "permiso_acceso",
                column: "codigo_permiso",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permiso_acceso_modulo",
                table: "permiso_acceso",
                column: "modulo");

            migrationBuilder.CreateIndex(
                name: "IX_rol_permiso_acceso_permiso_acceso_id",
                table: "rol_permiso_acceso",
                column: "permiso_acceso_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_permiso_acceso_permiso_acceso_id",
                table: "usuario_permiso_acceso",
                column: "permiso_acceso_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_permiso_acceso_user_id_es_denegado",
                table: "usuario_permiso_acceso",
                columns: new[] { "user_id", "es_denegado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rol_permiso_acceso");

            migrationBuilder.DropTable(
                name: "usuario_permiso_acceso");

            migrationBuilder.DropTable(
                name: "permiso_acceso");
        }
    }
}
