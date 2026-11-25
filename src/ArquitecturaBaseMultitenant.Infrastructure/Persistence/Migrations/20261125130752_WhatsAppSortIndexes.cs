using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppSortIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTeams_TenantId_Inbox_Id",
                schema: "tenant",
                table: "WhatsAppTeams",
                columns: new[] { "TenantId", "Inbox", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTeams_TenantId_NotifyAfterMinutes_Id",
                schema: "tenant",
                table: "WhatsAppTeams",
                columns: new[] { "TenantId", "NotifyAfterMinutes", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCustomers_TenantId_LastConversationAtUtc_Id",
                schema: "tenant",
                table: "WhatsAppCustomers",
                columns: new[] { "TenantId", "LastConversationAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCustomers_TenantId_WaId_Id",
                schema: "tenant",
                table: "WhatsAppCustomers",
                columns: new[] { "TenantId", "WaId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_Recipients_Id",
                schema: "tenant",
                table: "WhatsAppBroadcasts",
                columns: new[] { "TenantId", "Recipients", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_Status_Id",
                schema: "tenant",
                table: "WhatsAppBroadcasts",
                columns: new[] { "TenantId", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_TemplateName_Id",
                schema: "tenant",
                table: "WhatsAppBroadcasts",
                columns: new[] { "TenantId", "TemplateName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppTeams_NotifyAfterMinutes_Id",
                schema: "platform",
                table: "PlatformWhatsAppTeams",
                columns: new[] { "NotifyAfterMinutes", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppTeams_TenantId_Inbox_Id",
                schema: "tenant",
                table: "WhatsAppTeams");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppTeams_TenantId_NotifyAfterMinutes_Id",
                schema: "tenant",
                table: "WhatsAppTeams");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppCustomers_TenantId_LastConversationAtUtc_Id",
                schema: "tenant",
                table: "WhatsAppCustomers");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppCustomers_TenantId_WaId_Id",
                schema: "tenant",
                table: "WhatsAppCustomers");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_Recipients_Id",
                schema: "tenant",
                table: "WhatsAppBroadcasts");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_Status_Id",
                schema: "tenant",
                table: "WhatsAppBroadcasts");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppBroadcasts_TenantId_TemplateName_Id",
                schema: "tenant",
                table: "WhatsAppBroadcasts");

            migrationBuilder.DropIndex(
                name: "IX_PlatformWhatsAppTeams_NotifyAfterMinutes_Id",
                schema: "platform",
                table: "PlatformWhatsAppTeams");
        }
    }
}
