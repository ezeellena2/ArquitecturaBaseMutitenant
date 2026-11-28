using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppChatMessageSentFromPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SentFromPhone",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SentFromPhone",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SentFromPhone",
                schema: "tenant",
                table: "WhatsAppChatMessages");

            migrationBuilder.DropColumn(
                name: "SentFromPhone",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages");
        }
    }
}
