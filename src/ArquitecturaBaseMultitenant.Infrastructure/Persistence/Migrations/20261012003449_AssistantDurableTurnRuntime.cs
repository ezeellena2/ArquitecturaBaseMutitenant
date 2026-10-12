using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistantDurableTurnRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AdmissionVersion",
                schema: "platform",
                table: "PlatformAssistantTurns",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssistantMessageId",
                schema: "platform",
                table: "PlatformAssistantTurns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BindingId",
                schema: "platform",
                table: "PlatformAssistantTurns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid[]>(
                name: "ContextMessageIds",
                schema: "platform",
                table: "PlatformAssistantTurns",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string>(
                name: "EventsJson",
                schema: "platform",
                table: "PlatformAssistantTurns",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                schema: "platform",
                table: "PlatformAssistantTurns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserMessageId",
                schema: "platform",
                table: "PlatformAssistantTurns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "PrivacySubjectsKnown",
                schema: "platform",
                table: "PlatformAssistantMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid[]>(
                name: "SubjectUserIds",
                schema: "platform",
                table: "PlatformAssistantMessages",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<long>(
                name: "AdmissionVersion",
                schema: "tenant",
                table: "AssistantTurns",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssistantMessageId",
                schema: "tenant",
                table: "AssistantTurns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "BindingId",
                schema: "tenant",
                table: "AssistantTurns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid[]>(
                name: "ContextMessageIds",
                schema: "tenant",
                table: "AssistantTurns",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string>(
                name: "EventsJson",
                schema: "tenant",
                table: "AssistantTurns",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                schema: "tenant",
                table: "AssistantTurns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserMessageId",
                schema: "tenant",
                table: "AssistantTurns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "PrivacySubjectsKnown",
                schema: "tenant",
                table: "AssistantMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid[]>(
                name: "SubjectUserIds",
                schema: "tenant",
                table: "AssistantMessages",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateTable(
                name: "AssistantTurnWorks",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Access = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ScopeTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantKind = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SessionStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Culture = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeadlineAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StopRequested = table.Column<bool>(type: "boolean", nullable: false),
                    CallStarted = table.Column<bool>(type: "boolean", nullable: false),
                    CallSequence = table.Column<int>(type: "integer", nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderModelId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Effort = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Funding = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantTurnWorks", x => x.Id);
                });

            migrationBuilder.Sql("GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform.\"AssistantTurnWorks\" TO mt_app;");
            UpgradeLegacy(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantTurns_ConversationId_Active",
                schema: "platform",
                table: "PlatformAssistantTurns",
                column: "ConversationId",
                unique: true,
                filter: "\"Status\" = 'Pending' OR \"Status\" = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAssistantMessages_SubjectUserIds",
                schema: "platform",
                table: "PlatformAssistantMessages",
                column: "SubjectUserIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurns_TenantId_ConversationId_Active",
                schema: "tenant",
                table: "AssistantTurns",
                columns: new[] { "TenantId", "ConversationId" },
                unique: true,
                filter: "\"Status\" = 'Pending' OR \"Status\" = 'Running'");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantMessages_SubjectUserIds",
                schema: "tenant",
                table: "AssistantMessages",
                column: "SubjectUserIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurnWorks_OwnerUserId_ScopeTenantId_Id",
                schema: "platform",
                table: "AssistantTurnWorks",
                columns: new[] { "OwnerUserId", "ScopeTenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTurnWorks_Status_LeaseExpiresAtUtc_CreatedAtUtc_Id",
                schema: "platform",
                table: "AssistantTurnWorks",
                columns: new[] { "Status", "LeaseExpiresAtUtc", "CreatedAtUtc", "Id" });
        }

        private static void UpgradeLegacy(MigrationBuilder migrationBuilder)
        {
            // Only intact, uniquely bound Quick snapshots can attest legacy assistant content.
            // Purged/ambiguous copies stay unknown: no GUID scanning, invented subjects or actor.
            foreach (var (schema, prefix) in new[] { ("tenant", ""), ("platform", "Platform") })
            {
                var scoped = schema == "tenant";
                var scope = scoped ? "m.\"TenantId\" = scope_id AND" : "";
                var conversationScope = scoped ? "c.\"TenantId\" = m.\"TenantId\" AND" : "";
                var operationScope = scoped ? "d.\"TenantId\" = c.\"TenantId\" AND d.\"Access\" = c.\"Access\" AND" : "";
                var turnScope = scoped ? "t.\"TenantId\" = scope_id AND" : "";
                var workScope = scoped ? "w.\"ScopeTenantId\" = t.\"TenantId\"" : "w.\"ScopeTenantId\" IS NULL";
                var enter = scoped ? "FOR scope_id IN SELECT \"Id\" FROM platform.\"Tenants\" ORDER BY \"Id\" LOOP\nPERFORM set_config('app.tenant_id', scope_id::text, true);" : "";
                var leave = scoped ? "END LOOP;" : "";
                migrationBuilder.Sql($$"""
                    DO $upgrade_turns$
                    DECLARE scope_id uuid;
                    previous_scope text := current_setting('app.tenant_id', true);
                    BEGIN
                    {{enter}}
                    WITH matches AS MATERIALIZED (
                        SELECT m."Id", d."SubjectUserIds", count(*) OVER (PARTITION BY m."Id") origins
                        FROM {{schema}}."{{prefix}}AssistantMessages" m
                        JOIN {{schema}}."{{prefix}}AssistantConversations" c
                            ON {{conversationScope}} c."Id" = m."ConversationId"
                        JOIN {{schema}}."{{prefix}}AssistantDurableOperations" d
                            ON {{operationScope}} d."ConversationId" = c."Id" AND d."OwnerUserId" = c."OwnerUserId"
                        CROSS JOIN LATERAL jsonb_array_elements(CASE
                            WHEN jsonb_typeof(d."SnapshotJson" -> 'Messages') = 'array'
                                THEN d."SnapshotJson" -> 'Messages' ELSE '[]'::jsonb END) AS snapshot(message)
                        WHERE {{scope}} NOT m."PrivacySubjectsKnown" AND m."Role" = 'Assistant'
                            AND m."Status" = 'Complete' AND m."ToolKey" IS NULL
                            AND d."Kind" = 'assistant.quick' AND d."QuickKey" IS NOT NULL
                            AND d."SnapshotJson" #>> '{Conversation,Id}' = c."Id"::text
                            AND CASE WHEN jsonb_typeof(d."SnapshotJson" -> 'Messages') = 'array'
                                THEN jsonb_array_length(d."SnapshotJson" -> 'Messages') ELSE 0 END = 2
                            AND snapshot.message ->> 'Id' = m."Id"::text
                            AND snapshot.message ->> 'Role' = '1' AND snapshot.message ->> 'Status' = '0'
                            AND snapshot.message ->> 'Text' = m."Text"
                            AND d."SnapshotJson" #>> '{Result,Text}' = m."Text"
                            AND COALESCE(m."LinksJson", '[]'::jsonb) = snapshot.message -> 'Links'
                            AND snapshot.message -> 'Attachments' = '[]'::jsonb
                    )
                    UPDATE {{schema}}."{{prefix}}AssistantMessages" m
                    SET "SubjectUserIds" = proof."SubjectUserIds", "PrivacySubjectsKnown" = true
                    FROM matches proof WHERE {{scope}} m."Id" = proof."Id" AND proof.origins = 1;

                    -- Legacy stubs cannot be resumed: no trustworthy actor/funding work exists.
                    -- Close them before installing the active-turn uniqueness barrier.
                    UPDATE {{schema}}."{{prefix}}AssistantTurns" t
                    SET "Status" = 'Failed', "LeaseId" = NULL, "LeaseExpiresAtUtc" = NULL
                    WHERE {{turnScope}} t."Status" IN ('Pending', 'Running')
                        AND NOT EXISTS (SELECT 1 FROM platform."AssistantTurnWorks" w
                            WHERE w."Id" = t."Id" AND {{workScope}});
                    {{leave}}
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                    END
                    $upgrade_turns$;
                    """);
                foreach (var column in new[] { "AssistantMessageId", "BindingId", "ContextMessageIds", "EventsJson", "Sequence", "UserMessageId" })
                    migrationBuilder.Sql($"ALTER TABLE {schema}.\"{prefix}AssistantTurns\" ALTER COLUMN \"{column}\" DROP DEFAULT;");
                foreach (var column in new[] { "PrivacySubjectsKnown", "SubjectUserIds" })
                    migrationBuilder.Sql($"ALTER TABLE {schema}.\"{prefix}AssistantMessages\" ALTER COLUMN \"{column}\" DROP DEFAULT;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantTurnWorks",
                schema: "platform");

            migrationBuilder.DropIndex(
                name: "IX_PlatformAssistantTurns_ConversationId_Active",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropIndex(
                name: "IX_PlatformAssistantMessages_SubjectUserIds",
                schema: "platform",
                table: "PlatformAssistantMessages");

            migrationBuilder.DropIndex(
                name: "IX_AssistantTurns_TenantId_ConversationId_Active",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropIndex(
                name: "IX_AssistantMessages_SubjectUserIds",
                schema: "tenant",
                table: "AssistantMessages");

            migrationBuilder.DropColumn(
                name: "AdmissionVersion",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropColumn(
                name: "AssistantMessageId",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropColumn(
                name: "BindingId",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropColumn(
                name: "ContextMessageIds",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropColumn(
                name: "EventsJson",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropColumn(
                name: "UserMessageId",
                schema: "platform",
                table: "PlatformAssistantTurns");

            migrationBuilder.DropColumn(
                name: "PrivacySubjectsKnown",
                schema: "platform",
                table: "PlatformAssistantMessages");

            migrationBuilder.DropColumn(
                name: "SubjectUserIds",
                schema: "platform",
                table: "PlatformAssistantMessages");

            migrationBuilder.DropColumn(
                name: "AdmissionVersion",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "AssistantMessageId",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "BindingId",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "ContextMessageIds",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "EventsJson",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "UserMessageId",
                schema: "tenant",
                table: "AssistantTurns");

            migrationBuilder.DropColumn(
                name: "PrivacySubjectsKnown",
                schema: "tenant",
                table: "AssistantMessages");

            migrationBuilder.DropColumn(
                name: "SubjectUserIds",
                schema: "tenant",
                table: "AssistantMessages");
        }
    }
}
