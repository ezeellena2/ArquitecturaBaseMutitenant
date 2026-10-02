using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservedTenantSlugs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservedTenantSlugs",
                schema: "platform",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservedTenantSlugs", x => x.Slug);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "ReservedTenantSlugs",
                column: "Slug",
                values: new object[]
                {
                    "admin",
                    "api",
                    "app",
                    "auth",
                    "empresas",
                    "plataforma",
                    "www"
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservedTenantSlugs",
                schema: "platform");
        }
    }
}
