using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChannelId",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                schema: "identity",
                table: "WhatsAppMessages",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlatformWhatsAppChats",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastInboundAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AwaitingReply = table.Column<bool>(type: "boolean", nullable: false),
                    WaitingSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TakenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformWhatsAppChats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformWhatsAppChats_WhatsAppChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "identity",
                        principalTable: "WhatsAppChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppCustomers",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    WaId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Consent = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ConsentSource = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ConsentChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastConversationAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppCustomers", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppCustomers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformWhatsAppChatMessages",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Direction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorName = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: true),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    MediaKind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Wamid = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformWhatsAppChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformWhatsAppChatMessages_PlatformWhatsAppChats_ChatId",
                        column: x => x.ChatId,
                        principalSchema: "platform",
                        principalTable: "PlatformWhatsAppChats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppChats",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Inbox = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    WaId = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastMessageAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastInboundAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessagePreview = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AwaitingReply = table.Column<bool>(type: "boolean", nullable: false),
                    WaitingSinceUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TakenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppChats", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppChats_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppChats_WhatsAppChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "identity",
                        principalTable: "WhatsAppChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppChats_WhatsAppCustomers_TenantId_CustomerId",
                        columns: x => new { x.TenantId, x.CustomerId },
                        principalSchema: "tenant",
                        principalTable: "WhatsAppCustomers",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppChatMessages",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Direction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorName = table.Column<string>(type: "character varying(201)", maxLength: 201, nullable: true),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    MediaKind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Wamid = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppChatMessages", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_WhatsAppChatMessages_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppChatMessages_WhatsAppChats_TenantId_ChatId",
                        columns: x => new { x.TenantId, x.ChatId },
                        principalSchema: "tenant",
                        principalTable: "WhatsAppChats",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_ChannelId",
                schema: "identity",
                table: "WhatsAppMessages",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppChatMessages_ChatId_CreatedAtUtc_Id",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                columns: new[] { "ChatId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppChatMessages_OutboxMessageId",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                column: "OutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppChatMessages_Wamid",
                schema: "platform",
                table: "PlatformWhatsAppChatMessages",
                column: "Wamid",
                unique: true,
                filter: "\"Wamid\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppChats_ChannelId_WaId",
                schema: "platform",
                table: "PlatformWhatsAppChats",
                columns: new[] { "ChannelId", "WaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppChats_LastMessageAtUtc_Id",
                schema: "platform",
                table: "PlatformWhatsAppChats",
                columns: new[] { "LastMessageAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformWhatsAppChats_TeamId_WaitingSinceUtc",
                schema: "platform",
                table: "PlatformWhatsAppChats",
                columns: new[] { "TeamId", "WaitingSinceUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChatMessages_TenantId_ChatId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                columns: new[] { "TenantId", "ChatId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChatMessages_TenantId_OutboxMessageId",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                columns: new[] { "TenantId", "OutboxMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChatMessages_TenantId_Wamid",
                schema: "tenant",
                table: "WhatsAppChatMessages",
                columns: new[] { "TenantId", "Wamid" },
                unique: true,
                filter: "\"Wamid\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChats_ChannelId",
                schema: "tenant",
                table: "WhatsAppChats",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChats_TenantId_ChannelId_WaId_Inbox",
                schema: "tenant",
                table: "WhatsAppChats",
                columns: new[] { "TenantId", "ChannelId", "WaId", "Inbox" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChats_TenantId_CustomerId",
                schema: "tenant",
                table: "WhatsAppChats",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChats_TenantId_Inbox_LastMessageAtUtc_Id",
                schema: "tenant",
                table: "WhatsAppChats",
                columns: new[] { "TenantId", "Inbox", "LastMessageAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppChats_TenantId_Inbox_TeamId_WaitingSinceUtc",
                schema: "tenant",
                table: "WhatsAppChats",
                columns: new[] { "TenantId", "Inbox", "TeamId", "WaitingSinceUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCustomers_Tags",
                schema: "tenant",
                table: "WhatsAppCustomers",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCustomers_TenantId_Consent_Id",
                schema: "tenant",
                table: "WhatsAppCustomers",
                columns: new[] { "TenantId", "Consent", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCustomers_TenantId_Name_Id",
                schema: "tenant",
                table: "WhatsAppCustomers",
                columns: new[] { "TenantId", "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppCustomers_TenantId_WaId",
                schema: "tenant",
                table: "WhatsAppCustomers",
                columns: new[] { "TenantId", "WaId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WhatsAppMessages_WhatsAppChannels_ChannelId",
                schema: "identity",
                table: "WhatsAppMessages",
                column: "ChannelId",
                principalSchema: "identity",
                principalTable: "WhatsAppChannels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppCustomers");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppChats");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppChatMessages");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."WhatsAppCustomers",
                tenant."WhatsAppChats",
                tenant."WhatsAppChatMessages",
                platform."PlatformWhatsAppChats",
                platform."PlatformWhatsAppChatMessages" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WhatsAppMessages_WhatsAppChannels_ChannelId",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropTable(
                name: "PlatformWhatsAppChatMessages",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "WhatsAppChatMessages",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PlatformWhatsAppChats",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "WhatsAppChats",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "WhatsAppCustomers",
                schema: "tenant");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppMessages_ChannelId",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "ChannelId",
                schema: "identity",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "ContentType",
                schema: "identity",
                table: "WhatsAppMessages");
        }
    }
}
