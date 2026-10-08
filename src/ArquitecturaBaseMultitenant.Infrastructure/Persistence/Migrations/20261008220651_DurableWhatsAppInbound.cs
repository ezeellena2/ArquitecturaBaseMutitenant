using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableWhatsAppInbound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalMethodId",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalUserId",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyId",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_Direction_NextAttemptAtUtc_LeaseUntilUtc_R~",
                schema: "identity",
                table: "WhatsAppMessages",
                columns: new[] { "Direction", "NextAttemptAtUtc", "LeaseUntilUtc", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_OriginalUserId",
                schema: "identity",
                table: "WhatsAppMessages",
                column: "OriginalUserId");
            // Las entradas de T02 sobreviven la actualización; su propiedad anterior desconocida sigue null.
            migrationBuilder.Sql("""
                UPDATE identity."WhatsAppMessages" SET "NextAttemptAtUtc" = "ReceivedAtUtc"
                WHERE "Direction" = 'Inbound' AND "EventType" = 'inbound' AND "ProcessedAtUtc" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppMessages_Direction_NextAttemptAtUtc_LeaseUntilUtc_R~",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppMessages_OriginalUserId",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "OriginalMethodId",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "OriginalUserId",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "ReplyId",
                schema: "identity",
                table: "WhatsAppMessages");
        }
    }
}
