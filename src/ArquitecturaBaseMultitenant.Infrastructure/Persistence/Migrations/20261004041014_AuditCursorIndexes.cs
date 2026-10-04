using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditCursorIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_OccurredAtUtc_Id",
                schema: "platform",
                table: "SecurityEvents",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginAudits_OccurredAtUtc_Id",
                schema: "identity",
                table: "LoginAudits",
                columns: new[] { "OccurredAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_OccurredAtUtc_Id",
                schema: "platform",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_LoginAudits_OccurredAtUtc_Id",
                schema: "identity",
                table: "LoginAudits");
        }
    }
}
