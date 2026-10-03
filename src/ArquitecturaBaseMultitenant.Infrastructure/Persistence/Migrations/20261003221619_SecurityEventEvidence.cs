using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityEventEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Changes",
                schema: "platform",
                table: "SecurityEvents",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "MaskedIpAddress",
                schema: "platform",
                table: "SecurityEvents",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetAccountId",
                schema: "platform",
                table: "SecurityEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_TargetAccountId_OccurredAtUtc",
                schema: "platform",
                table: "SecurityEvents",
                columns: new[] { "TargetAccountId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_TargetAccountId_OccurredAtUtc",
                schema: "platform",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "Changes",
                schema: "platform",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "MaskedIpAddress",
                schema: "platform",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "TargetAccountId",
                schema: "platform",
                table: "SecurityEvents");
        }
    }
}
