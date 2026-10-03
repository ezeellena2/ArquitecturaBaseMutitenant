using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformOperatorRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformRoleAssignments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRoleAssignments_UserId",
                schema: "platform",
                table: "PlatformRoleAssignments",
                column: "UserId",
                unique: true);

            // Conserva los operadores anteriores al modelo Owner/Support sin crear cuentas ni espacios.
            migrationBuilder.Sql("""
                INSERT INTO platform."PlatformRoleAssignments" ("Id", "UserId", "Role")
                SELECT uuidv7(), "Id", 'Owner'
                FROM identity."AspNetUsers"
                WHERE "IsPlatformOperator" = true;

                GRANT SELECT, INSERT, UPDATE, DELETE ON platform."PlatformRoleAssignments" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformRoleAssignments",
                schema: "platform");
        }
    }
}
