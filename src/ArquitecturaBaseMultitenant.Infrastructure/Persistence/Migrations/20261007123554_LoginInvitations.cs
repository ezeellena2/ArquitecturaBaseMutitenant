using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoginInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoginInvitations",
                schema: "identity",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginInvitations", x => new { x.TenantId, x.InvitationId });
                    table.ForeignKey(
                        name: "FK_LoginInvitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginInvitations_Destination_ExpiresAtUtc",
                schema: "identity",
                table: "LoginInvitations",
                columns: new[] { "Destination", "ExpiresAtUtc" });

            migrationBuilder.Sql("""
                CREATE FUNCTION identity.sync_login_invitation() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' OR NEW."Status" <> 'Pending' THEN
                        DELETE FROM identity."LoginInvitations"
                        WHERE "TenantId" = OLD."TenantId" AND "InvitationId" = OLD."Id";
                        RETURN NULL;
                    END IF;
                    INSERT INTO identity."LoginInvitations"
                        ("TenantId", "InvitationId", "MemberId", "Destination", "IssuedAtUtc", "ExpiresAtUtc")
                    VALUES (NEW."TenantId", NEW."Id", NEW."MemberId", NEW."Destination", NEW."IssuedAtUtc", NEW."ExpiresAtUtc")
                    ON CONFLICT ("TenantId", "InvitationId") DO UPDATE SET
                        "MemberId" = EXCLUDED."MemberId", "Destination" = EXCLUDED."Destination",
                        "IssuedAtUtc" = EXCLUDED."IssuedAtUtc", "ExpiresAtUtc" = EXCLUDED."ExpiresAtUtc";
                    RETURN NULL;
                END
                $function$;
                REVOKE ALL ON FUNCTION identity.sync_login_invitation() FROM PUBLIC;
                CREATE TRIGGER sync_login_invitation AFTER INSERT OR UPDATE OR DELETE ON tenant."Invitations"
                    FOR EACH ROW EXECUTE FUNCTION identity.sync_login_invitation();
                GRANT SELECT ON identity."LoginInvitations" TO mt_app;

                -- Bootstrap de datos existentes bajo cada alcance RLS, sin deshabilitarlo.
                DO $bootstrap$
                DECLARE organization_id uuid;
                DECLARE previous_scope text := current_setting('app.tenant_id', true);
                BEGIN
                    FOR organization_id IN SELECT "Id" FROM platform."Tenants" WHERE "Kind" = 'Business' LOOP
                        PERFORM set_config('app.tenant_id', organization_id::text, true);
                        INSERT INTO identity."LoginInvitations"
                            ("TenantId", "InvitationId", "MemberId", "Destination", "IssuedAtUtc", "ExpiresAtUtc")
                        SELECT "TenantId", "Id", "MemberId", "Destination", "IssuedAtUtc", "ExpiresAtUtc"
                        FROM tenant."Invitations" WHERE "TenantId" = organization_id AND "Status" = 'Pending';
                    END LOOP;
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                END
                $bootstrap$;

                -- Capacidad por OTP: destino y claves compuestas exactas, sólo reservas y sus miembros.
                CREATE POLICY login_invitation_read ON tenant."Invitations" FOR SELECT TO mt_app USING (
                    "Destination" = nullif(current_setting('app.login_invitation_destination', true), '')
                    AND ("TenantId"::text || ':' || "Id"::text) = ANY(string_to_array(
                        nullif(current_setting('app.login_invitation_keys', true), ''), ',')));
                CREATE POLICY login_invitation_accept ON tenant."Invitations" FOR UPDATE TO mt_app USING (
                    "Destination" = nullif(current_setting('app.login_invitation_destination', true), '')
                    AND ("TenantId"::text || ':' || "Id"::text) = ANY(string_to_array(
                        nullif(current_setting('app.login_invitation_keys', true), ''), ',')))
                WITH CHECK (
                    "Destination" = nullif(current_setting('app.login_invitation_destination', true), '')
                    AND ("TenantId"::text || ':' || "Id"::text) = ANY(string_to_array(
                        nullif(current_setting('app.login_invitation_keys', true), ''), ','))
                    AND "Status" = 'Accepted' AND "AcceptedByUserId" =
                        nullif(current_setting('app.login_invitation_user_id', true), '')::uuid);
                CREATE POLICY login_invitation_member_read ON tenant."Members" FOR SELECT TO mt_app USING (
                    ("TenantId"::text || ':' || "Id"::text) = ANY(string_to_array(
                        nullif(current_setting('app.login_invitation_member_keys', true), ''), ','))
                    AND EXISTS (SELECT 1 FROM tenant."Invitations" invitation
                        WHERE invitation."TenantId" = "Members"."TenantId" AND invitation."MemberId" = "Members"."Id"
                        AND invitation."Destination" = nullif(current_setting('app.login_invitation_destination', true), '')
                        AND (invitation."TenantId"::text || ':' || invitation."Id"::text) = ANY(string_to_array(
                            nullif(current_setting('app.login_invitation_keys', true), ''), ','))));
                CREATE POLICY login_invitation_member_accept ON tenant."Members" FOR UPDATE TO mt_app USING (
                    ("TenantId"::text || ':' || "Id"::text) = ANY(string_to_array(
                        nullif(current_setting('app.login_invitation_member_keys', true), ''), ','))
                    AND EXISTS (SELECT 1 FROM tenant."Invitations" invitation
                        WHERE invitation."TenantId" = "Members"."TenantId" AND invitation."MemberId" = "Members"."Id"
                        AND invitation."Destination" = nullif(current_setting('app.login_invitation_destination', true), '')
                        AND (invitation."TenantId"::text || ':' || invitation."Id"::text) = ANY(string_to_array(
                            nullif(current_setting('app.login_invitation_keys', true), ''), ','))))
                WITH CHECK (
                    ("TenantId"::text || ':' || "Id"::text) = ANY(string_to_array(
                        nullif(current_setting('app.login_invitation_member_keys', true), ''), ','))
                    AND "Status" = 'Active' AND "UserId" =
                        nullif(current_setting('app.login_invitation_user_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY login_invitation_member_accept ON tenant."Members";
                DROP POLICY login_invitation_member_read ON tenant."Members";
                DROP POLICY login_invitation_accept ON tenant."Invitations";
                DROP POLICY login_invitation_read ON tenant."Invitations";
                DROP TRIGGER sync_login_invitation ON tenant."Invitations";
                DROP FUNCTION identity.sync_login_invitation();
                """);
            migrationBuilder.DropTable(
                name: "LoginInvitations",
                schema: "identity");
        }
    }
}
