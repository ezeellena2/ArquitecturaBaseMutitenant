using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppConversationLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "engagement");

            migrationBuilder.AddColumn<DateTime>(
                name: "LinkOfferedAtUtc",
                schema: "tenant",
                table: "WhatsAppCustomers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppConversationLinks",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ConsumerName = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: true),
                    LinkedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppConversationLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversationLinks_Tenants_BusinessTenantId",
                        column: x => x.BusinessTenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversationLinks_Tenants_ConsumerTenantId",
                        column: x => x.ConsumerTenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppConversationLinks_WhatsAppChats_BusinessTenantId_Ch~",
                        columns: x => new { x.BusinessTenantId, x.ChatId },
                        principalSchema: "tenant",
                        principalTable: "WhatsAppChats",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppLinkedMessages",
                schema: "engagement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    MediaKind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppLinkedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppLinkedMessages_WhatsAppConversationLinks_LinkId",
                        column: x => x.LinkId,
                        principalSchema: "engagement",
                        principalTable: "WhatsAppConversationLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversationLinks_BusinessTenantId_ChatId",
                schema: "engagement",
                table: "WhatsAppConversationLinks",
                columns: new[] { "BusinessTenantId", "ChatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConversationLinks_ConsumerTenantId_LastMessageAtUtc~",
                schema: "engagement",
                table: "WhatsAppConversationLinks",
                columns: new[] { "ConsumerTenantId", "LastMessageAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppLinkedMessages_BusinessTenantId",
                schema: "engagement",
                table: "WhatsAppLinkedMessages",
                column: "BusinessTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppLinkedMessages_ConsumerTenantId",
                schema: "engagement",
                table: "WhatsAppLinkedMessages",
                column: "ConsumerTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppLinkedMessages_LinkId_CreatedAtUtc_Id",
                schema: "engagement",
                table: "WhatsAppLinkedMessages",
                columns: new[] { "LinkId", "CreatedAtUtc", "Id" });

            migrationBuilder.EnablePartiesRls(Schemas.Engagement, "WhatsAppConversationLinks");
            migrationBuilder.EnablePartiesRls(Schemas.Engagement, "WhatsAppLinkedMessages");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                engagement."WhatsAppConversationLinks",
                engagement."WhatsAppLinkedMessages" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppLinkedMessages",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "WhatsAppConversationLinks",
                schema: "engagement");

            migrationBuilder.DropColumn(
                name: "LinkOfferedAtUtc",
                schema: "tenant",
                table: "WhatsAppCustomers");
        }
    }
}
