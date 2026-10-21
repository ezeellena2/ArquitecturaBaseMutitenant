using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantConversationActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlatformAssistantConversations_OwnerUserId_LastActivityAtUt~",
                schema: "platform",
                table: "PlatformAssistantConversations");

            migrationBuilder.DropIndex(
                name: "IX_AssistantConversations_TenantId_LastActivityAtUtc_Id",
                schema: "tenant",
                table: "AssistantConversations");

            migrationBuilder.DropIndex(
                name: "IX_AssistantConversations_TenantId_OwnerUserId_Access_LastActi~",
                schema: "tenant",
                table: "AssistantConversations");

            migrationBuilder.CreateTable(
                name: "AssistantConversationActivities",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AutomaticTitle = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantConversationActivities", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_AssistantConversationActivities_AssistantConversations_Tena~",
                        columns: x => new { x.TenantId, x.Id },
                        principalSchema: "tenant",
                        principalTable: "AssistantConversations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssistantConversationActivities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.EnableTenantRls("tenant", "AssistantConversationActivities");

            migrationBuilder.CreateTable(
                name: "PlatformAssistantConversationActivities",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    AutomaticTitle = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAssistantConversationActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformAssistantConversationActivities_PlatformAssistantCo~",
                        column: x => x.Id,
                        principalSchema: "platform",
                        principalTable: "PlatformAssistantConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                DO $activity_upgrade$
                DECLARE scope_id uuid;
                previous_scope text := current_setting('app.tenant_id', true);
                BEGIN
                    -- El administrador también respeta RLS: las copias conservan su alcance original.
                    FOR scope_id IN SELECT "Id" FROM platform."Tenants" ORDER BY "Id" LOOP
                        PERFORM set_config('app.tenant_id', scope_id::text, true);
                        INSERT INTO tenant."AssistantConversationActivities"
                            ("TenantId", "Id", "OwnerUserId", "Access", "AutomaticTitle", "LastActivityAtUtc")
                        SELECT "TenantId", "Id", "OwnerUserId", "Access",
                            CASE WHEN "IsTitleManual" THEN NULL ELSE "Title" END, "LastActivityAtUtc"
                        FROM tenant."AssistantConversations" WHERE "TenantId" = scope_id;
                        UPDATE tenant."AssistantConversations" SET "Title" = NULL
                        WHERE "TenantId" = scope_id AND NOT "IsTitleManual" AND "Title" IS NOT NULL;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                    INSERT INTO platform."PlatformAssistantConversationActivities"
                        ("Id", "OwnerUserId", "Access", "AutomaticTitle", "LastActivityAtUtc")
                    SELECT "Id", "OwnerUserId", 'Platform',
                        CASE WHEN "IsTitleManual" THEN NULL ELSE "Title" END, "LastActivityAtUtc"
                    FROM platform."PlatformAssistantConversations";
                    UPDATE platform."PlatformAssistantConversations" SET "Title" = NULL
                    WHERE NOT "IsTitleManual" AND "Title" IS NOT NULL;
                END
                $activity_upgrade$;
                """);

            migrationBuilder.DropColumn(
                name: "LastActivityAtUtc",
                schema: "platform",
                table: "PlatformAssistantConversations");

            migrationBuilder.DropColumn(
                name: "LastActivityAtUtc",
                schema: "tenant",
                table: "AssistantConversations");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversationActivities_TenantId_LastActivityAtUtc_~",
                schema: "tenant",
                table: "AssistantConversationActivities",
                columns: new[] { "TenantId", "LastActivityAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversationActivities_TenantId_OwnerUserId_Access~",
                schema: "tenant",
                table: "AssistantConversationActivities",
                columns: new[] { "TenantId", "OwnerUserId", "Access", "LastActivityAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantConversationActivities_OwnerUserId_LastAct~",
                schema: "platform",
                table: "PlatformAssistantConversationActivities",
                columns: new[] { "OwnerUserId", "LastActivityAtUtc", "Id" });

            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE
                tenant."AssistantConversationActivities", platform."PlatformAssistantConversationActivities" TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAtUtc",
                schema: "platform",
                table: "PlatformAssistantConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAtUtc",
                schema: "tenant",
                table: "AssistantConversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                DO $activity_downgrade$
                DECLARE scope_id uuid;
                previous_scope text := current_setting('app.tenant_id', true);
                BEGIN
                    FOR scope_id IN SELECT "Id" FROM platform."Tenants" ORDER BY "Id" LOOP
                        PERFORM set_config('app.tenant_id', scope_id::text, true);
                        UPDATE tenant."AssistantConversations" c
                        SET "Title" = coalesce(c."Title", (SELECT a."AutomaticTitle"
                                FROM tenant."AssistantConversationActivities" a WHERE a."TenantId" = c."TenantId" AND a."Id" = c."Id")),
                            "LastActivityAtUtc" = coalesce((SELECT a."LastActivityAtUtc"
                                FROM tenant."AssistantConversationActivities" a WHERE a."TenantId" = c."TenantId" AND a."Id" = c."Id"), c."CreatedAtUtc")
                        WHERE c."TenantId" = scope_id;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                    UPDATE platform."PlatformAssistantConversations" c
                    SET "Title" = coalesce(c."Title", (SELECT a."AutomaticTitle"
                            FROM platform."PlatformAssistantConversationActivities" a WHERE a."Id" = c."Id")),
                        "LastActivityAtUtc" = coalesce((SELECT a."LastActivityAtUtc"
                            FROM platform."PlatformAssistantConversationActivities" a WHERE a."Id" = c."Id"), c."CreatedAtUtc");
                END
                $activity_downgrade$;
                """);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActivityAtUtc",
                schema: "platform",
                table: "PlatformAssistantConversations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastActivityAtUtc",
                schema: "tenant",
                table: "AssistantConversations",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "AssistantConversationActivities",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "PlatformAssistantConversationActivities",
                schema: "platform");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantConversations_OwnerUserId_LastActivityAtUt~",
                schema: "platform",
                table: "PlatformAssistantConversations",
                columns: new[] { "OwnerUserId", "LastActivityAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversations_TenantId_LastActivityAtUtc_Id",
                schema: "tenant",
                table: "AssistantConversations",
                columns: new[] { "TenantId", "LastActivityAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantConversations_TenantId_OwnerUserId_Access_LastActi~",
                schema: "tenant",
                table: "AssistantConversations",
                columns: new[] { "TenantId", "OwnerUserId", "Access", "LastActivityAtUtc", "Id" });
        }
    }
}
