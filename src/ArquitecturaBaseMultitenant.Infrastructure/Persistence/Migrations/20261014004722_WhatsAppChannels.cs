using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppChannels",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneNumberId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    WabaId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DisplayNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    VerifiedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Coexistence = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ConsultationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UsageTimeZoneId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    EncryptedToken = table.Column<byte[]>(type: "bytea", nullable: false),
                    EncryptedPin = table.Column<byte[]>(type: "bytea", nullable: true),
                    ConnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisconnectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppChannels_Tenants_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppChannelUsages",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceDelivered = table.Column<int>(type: "integer", nullable: false),
                    ServiceCharged = table.Column<int>(type: "integer", nullable: false),
                    OtherDelivered = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppChannelUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppChannelUsages_WhatsAppChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "identity",
                        principalTable: "WhatsAppChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChannels_OrganizationId_DisplayNumber_Id",
                schema: "identity",
                table: "WhatsAppChannels",
                columns: new[] { "OrganizationId", "DisplayNumber", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChannels_PhoneNumberId_Active",
                schema: "identity",
                table: "WhatsAppChannels",
                column: "PhoneNumberId",
                unique: true,
                filter: "\"Status\" <> 'Disconnected'");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChannels_WabaId",
                schema: "identity",
                table: "WhatsAppChannels",
                column: "WabaId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChannelUsages_ChannelId_Month",
                schema: "identity",
                table: "WhatsAppChannelUsages",
                columns: new[] { "ChannelId", "Month" },
                unique: true);

            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE ON TABLE identity.\"WhatsAppChannels\" TO mt_app;");
            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE ON TABLE identity.\"WhatsAppChannelUsages\" TO mt_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppChannelUsages",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "WhatsAppChannels",
                schema: "identity");
        }
    }
}
