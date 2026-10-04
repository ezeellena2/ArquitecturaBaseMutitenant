using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountRecoveryDeferredNotice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccountNotifiedAtUtc",
                schema: "platform",
                table: "AccountRecoveryRequests",
                type: "timestamp with time zone",
                nullable: true);

            // Los pedidos anteriores ya avisaron a su cuenta al registrarse: el trabajo nuevo no los repite.
            migrationBuilder.Sql("""
                UPDATE platform."AccountRecoveryRequests" SET "AccountNotifiedAtUtc" = "CreatedAtUtc";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryRequests_CreatedAtUtc_Id",
                schema: "platform",
                table: "AccountRecoveryRequests",
                columns: new[] { "CreatedAtUtc", "Id" },
                filter: "\"AccountNotifiedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountRecoveryRequests_CreatedAtUtc_Id",
                schema: "platform",
                table: "AccountRecoveryRequests");

            migrationBuilder.DropColumn(
                name: "AccountNotifiedAtUtc",
                schema: "platform",
                table: "AccountRecoveryRequests");
        }
    }
}
