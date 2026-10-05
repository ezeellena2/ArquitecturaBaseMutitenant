using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformTenantDetailDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiresOn",
                schema: "platform",
                table: "TenantFeatures",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                schema: "public_site",
                table: "PublicPages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresOn",
                schema: "platform",
                table: "TenantFeatures");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                schema: "public_site",
                table: "PublicPages");
        }
    }
}
