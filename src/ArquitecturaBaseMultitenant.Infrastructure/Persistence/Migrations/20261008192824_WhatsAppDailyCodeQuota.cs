using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppDailyCodeQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppCodeQuotas",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DayUtc = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppCodeQuotas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCodeQuotas_PhoneNumberId_DayUtc",
                schema: "identity",
                table: "WhatsAppCodeQuotas",
                columns: new[] { "PhoneNumberId", "DayUtc" },
                unique: true);
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity.\"WhatsAppCodeQuotas\" TO mt_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppCodeQuotas",
                schema: "identity");
        }
    }
}
