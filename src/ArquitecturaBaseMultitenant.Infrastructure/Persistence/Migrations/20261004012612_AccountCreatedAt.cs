using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                schema: "identity",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            // Las cuentas existentes recuperan el alta del instante que guarda su id UUIDv7; otro id queda null.
            migrationBuilder.Sql("""
                UPDATE identity."AspNetUsers" SET "CreatedAtUtc" = uuid_extract_timestamp("Id")
                WHERE "CreatedAtUtc" IS NULL AND uuid_extract_version("Id") = 7;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "identity",
                table: "AspNetUsers");
        }
    }
}
