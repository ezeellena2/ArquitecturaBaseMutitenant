using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhatsAppContacts",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WaId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MethodId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastInboundAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppContacts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WhatsAppContacts_LoginMethods_MethodId",
                        column: x => x.MethodId,
                        principalSchema: "identity",
                        principalTable: "LoginMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMessages",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Wamid = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Direction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EncryptedText = table.Column<string>(type: "text", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppMessages_WhatsAppContacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "identity",
                        principalTable: "WhatsAppContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppContacts_MethodId",
                schema: "identity",
                table: "WhatsAppContacts",
                column: "MethodId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppContacts_UserId",
                schema: "identity",
                table: "WhatsAppContacts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppContacts_WaId",
                schema: "identity",
                table: "WhatsAppContacts",
                column: "WaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ContactId",
                schema: "identity",
                table: "WhatsAppMessages",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_PhoneNumberId_Wamid_EventType",
                schema: "identity",
                table: "WhatsAppMessages",
                columns: new[] { "PhoneNumberId", "Wamid", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ProcessedAtUtc_LeaseUntilUtc_ReceivedAtUtc",
                schema: "identity",
                table: "WhatsAppMessages",
                columns: new[] { "ProcessedAtUtc", "LeaseUntilUtc", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ReceivedAtUtc",
                schema: "identity",
                table: "WhatsAppMessages",
                column: "ReceivedAtUtc");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE identity."WhatsAppContacts", identity."WhatsAppMessages" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppMessages",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "WhatsAppContacts",
                schema: "identity");
        }
    }
}
