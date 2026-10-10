using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantDurableConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LinksJson",
                schema: "platform",
                table: "PlatformAssistantMessages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTitleManual",
                schema: "platform",
                table: "PlatformAssistantConversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                schema: "platform",
                table: "IdempotencyKeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceKind",
                schema: "platform",
                table: "IdempotencyKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseLocation",
                schema: "platform",
                table: "IdempotencyKeys",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinksJson",
                schema: "tenant",
                table: "AssistantMessages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTitleManual",
                schema: "tenant",
                table: "AssistantConversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AssistantDurableOperations",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    BodyHash = table.Column<string>(type: "text", nullable: false),
                    Route = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    QuickKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantDurableOperations", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantDurableOperations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantFileCleanups",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantFileCleanups", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantFileCleanups_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAssistantDurableOperations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<Guid>(type: "uuid", nullable: false),
                    BodyHash = table.Column<string>(type: "text", nullable: false),
                    Route = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: true),
                    QuickKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantDurableOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAssistantFileCleanups",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantFileCleanups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDurableOperations_TenantId_ConversationId_Id",
                schema: "tenant",
                table: "AssistantDurableOperations",
                columns: new[] { "TenantId", "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantDurableOperations_TenantId_OwnerUserId_Access_Key",
                schema: "tenant",
                table: "AssistantDurableOperations",
                columns: new[] { "TenantId", "OwnerUserId", "Access", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantFileCleanups_TenantId_CreatedAtUtc_Id",
                schema: "tenant",
                table: "AssistantFileCleanups",
                columns: new[] { "TenantId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantDurableOperations_OwnerUserId_Conversation~",
                schema: "platform",
                table: "PlatformAssistantDurableOperations",
                columns: new[] { "OwnerUserId", "ConversationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantDurableOperations_OwnerUserId_Key",
                schema: "platform",
                table: "PlatformAssistantDurableOperations",
                columns: new[] { "OwnerUserId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantFileCleanups_CreatedAtUtc_Id",
                schema: "platform",
                table: "PlatformAssistantFileCleanups",
                columns: new[] { "CreatedAtUtc", "Id" });
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantDurableOperations");
            migrationBuilder.EnableTenantRls(Schemas.Tenant, "AssistantFileCleanups");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."AssistantDurableOperations", tenant."AssistantFileCleanups",
                platform."PlatformAssistantDurableOperations", platform."PlatformAssistantFileCleanups" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantDurableOperations",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "AssistantFileCleanups",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PlatformAssistantDurableOperations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformAssistantFileCleanups",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "LinksJson",
                schema: "platform",
                table: "PlatformAssistantMessages");

            migrationBuilder.DropColumn(
                name: "IsTitleManual",
                schema: "platform",
                table: "PlatformAssistantConversations");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                schema: "platform",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "ReferenceKind",
                schema: "platform",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "ResponseLocation",
                schema: "platform",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "LinksJson",
                schema: "tenant",
                table: "AssistantMessages");

            migrationBuilder.DropColumn(
                name: "IsTitleManual",
                schema: "tenant",
                table: "AssistantConversations");

        }
    }
}
