using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformContactEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SalesEmail",
                schema: "platform",
                table: "PlatformSettings",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportEmail",
                schema: "platform",
                table: "PlatformSettings",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SalesEmail",
                schema: "platform",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "SupportEmail",
                schema: "platform",
                table: "PlatformSettings");
        }
    }
}
