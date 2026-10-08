using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableOutboxLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastErrorCode",
                schema: "platform",
                table: "OutboxMessages",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresAtUtc",
                schema: "platform",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                schema: "platform",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMessageId",
                schema: "platform",
                table: "OutboxMessages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Los mensajes históricos terminales ya no necesitan su contenido cifrado.
            migrationBuilder.Sql("""
                UPDATE platform."OutboxMessages" SET "EncryptedPayload" = ''
                WHERE "Status" IN ('Sent', 'Cancelled')
                   OR ("Status" = 'Failed' AND "NextAttemptAtUtc" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastErrorCode",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresAtUtc",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseId",
                schema: "platform",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "ProviderMessageId",
                schema: "platform",
                table: "OutboxMessages");
        }
    }
}
