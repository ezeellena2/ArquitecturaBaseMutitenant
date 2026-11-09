using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireLoginLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Base de la 12 (179): ningún mensaje lleva un enlace secreto. Los enlaces vivían 10 minutos: no hay qué conservar.
            migrationBuilder.DropTable(
                name: "LoginLinks",
                schema: "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Vuelve a la tabla de la E8, vacía y con sus permisos: la vuelta atrás no borra ni pierde nada.
            migrationBuilder.CreateTable(
                name: "LoginLinks",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    InvalidatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginLinks_TokenHash",
                schema: "identity",
                table: "LoginLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginLinks_UserId_CreatedAtUtc",
                schema: "identity",
                table: "LoginLinks",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.Sql("""GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."LoginLinks" TO mt_app;""");
        }
    }
}
