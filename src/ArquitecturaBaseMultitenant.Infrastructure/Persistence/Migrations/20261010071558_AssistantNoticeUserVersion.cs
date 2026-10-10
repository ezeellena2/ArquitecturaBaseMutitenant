using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantNoticeUserVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // T02 allowed one acceptance per access. Keep the first explicit acknowledgement,
            // with its original ID, date and access provenance; deterministic tie-break by ID.
            migrationBuilder.Sql("""
                DELETE FROM platform."AssistantNoticeAcceptances" AS duplicate
                USING (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "OwnerUserId", "NoticeVersion"
                        ORDER BY "AcceptedAtUtc", "Id") AS ordinal
                    FROM platform."AssistantNoticeAcceptances"
                ) AS ranked
                WHERE duplicate."Id" = ranked."Id" AND ranked.ordinal > 1;
                """);
            migrationBuilder.DropIndex(
                name: "IX_AssistantNoticeAcceptances_OwnerUserId_Access_NoticeVersion",
                schema: "platform",
                table: "AssistantNoticeAcceptances");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoticeAcceptances_OwnerUserId_NoticeVersion",
                schema: "platform",
                table: "AssistantNoticeAcceptances",
                columns: new[] { "OwnerUserId", "NoticeVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restores the old index only. Consolidated duplicate acknowledgements are not recreated.
            migrationBuilder.DropIndex(
                name: "IX_AssistantNoticeAcceptances_OwnerUserId_NoticeVersion",
                schema: "platform",
                table: "AssistantNoticeAcceptances");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoticeAcceptances_OwnerUserId_Access_NoticeVersion",
                schema: "platform",
                table: "AssistantNoticeAcceptances",
                columns: new[] { "OwnerUserId", "Access", "NoticeVersion" },
                unique: true);
        }
    }
}
