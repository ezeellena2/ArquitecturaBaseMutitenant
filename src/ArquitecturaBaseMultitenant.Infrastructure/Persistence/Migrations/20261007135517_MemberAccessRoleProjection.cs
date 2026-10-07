using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MemberAccessRoleProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberAccessRoles",
                schema: "identity",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberAccessRoles", x => new { x.TenantId, x.MemberId });
                    table.ForeignKey(
                        name: "FK_MemberAccessRoles_Members_TenantId_MemberId",
                        columns: x => new { x.TenantId, x.MemberId },
                        principalSchema: "tenant",
                        principalTable: "Members",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.Sql("""
                REVOKE ALL ON TABLE identity."MemberAccessRoles" FROM PUBLIC, mt_app;

                CREATE OR REPLACE FUNCTION identity.refresh_user_tenant_access_role(p_tenant_id uuid, p_member_id uuid)
                RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                DECLARE current_user_id uuid;
                DECLARE current_role_name text;
                BEGIN
                    -- Sólo una modificación dentro del alcance legítimo puede reconstruir el rol reservado.
                    IF p_tenant_id IS DISTINCT FROM nullif(current_setting('app.tenant_id', true), '')::uuid THEN
                        RETURN;
                    END IF;
                    SELECT member."UserId" INTO current_user_id
                    FROM tenant."Members" AS member
                    WHERE member."TenantId" = p_tenant_id AND member."Id" = p_member_id;
                    IF NOT FOUND THEN RETURN; END IF;

                    SELECT role."Name" INTO current_role_name
                    FROM tenant."RoleAssignments" AS assignment
                    JOIN tenant."Roles" AS role
                      ON role."TenantId" = assignment."TenantId" AND role."Id" = assignment."RoleId"
                    WHERE assignment."TenantId" = p_tenant_id AND assignment."MemberId" = p_member_id
                      AND assignment."CompanyId" IS NULL AND role."IsSystemRole"
                    ORDER BY CASE WHEN role."Name" = 'TenantAdmin' THEN 0 ELSE 1 END, role."Name"
                    LIMIT 1;

                    IF current_role_name IS NULL THEN
                        DELETE FROM identity."MemberAccessRoles"
                        WHERE "TenantId" = p_tenant_id AND "MemberId" = p_member_id;
                    ELSE
                        INSERT INTO identity."MemberAccessRoles" ("TenantId", "MemberId", "RoleName")
                        VALUES (p_tenant_id, p_member_id, current_role_name)
                        ON CONFLICT ("TenantId", "MemberId") DO UPDATE SET "RoleName" = EXCLUDED."RoleName";
                    END IF;
                    IF current_user_id IS NOT NULL THEN
                        UPDATE identity."UserTenantAccesses" SET "RoleName" = current_role_name
                        WHERE "UserId" = current_user_id AND "TenantId" = p_tenant_id;
                    END IF;
                END
                $function$;

                CREATE OR REPLACE FUNCTION identity.sync_user_tenant_access() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        DELETE FROM identity."UserTenantAccesses"
                        WHERE "UserId" = OLD."UserId" AND "TenantId" = OLD."TenantId";
                        RETURN OLD;
                    END IF;
                    IF TG_OP = 'UPDATE' AND
                       (OLD."UserId" IS DISTINCT FROM NEW."UserId" OR OLD."TenantId" IS DISTINCT FROM NEW."TenantId") THEN
                        DELETE FROM identity."UserTenantAccesses"
                        WHERE "UserId" = OLD."UserId" AND "TenantId" = OLD."TenantId";
                    END IF;
                    IF NEW."UserId" IS NULL THEN RETURN NEW; END IF;

                    -- NEW ya fue autorizado por RLS; el rol viene de la reserva, sin consultar tablas privadas ajenas.
                    INSERT INTO identity."UserTenantAccesses" ("UserId", "TenantId", "Status", "JoinedAtUtc", "RoleName")
                    VALUES (NEW."UserId", NEW."TenantId", NEW."Status", NEW."JoinedAtUtc",
                        (SELECT "RoleName" FROM identity."MemberAccessRoles"
                         WHERE "TenantId" = NEW."TenantId" AND "MemberId" = NEW."Id"))
                    ON CONFLICT ("UserId", "TenantId") DO UPDATE
                    SET "Status" = EXCLUDED."Status", "JoinedAtUtc" = EXCLUDED."JoinedAtUtc", "RoleName" = EXCLUDED."RoleName";
                    RETURN NEW;
                END
                $function$;
                REVOKE ALL ON FUNCTION identity.refresh_user_tenant_access_role(uuid, uuid) FROM PUBLIC, mt_app;
                REVOKE ALL ON FUNCTION identity.sync_user_tenant_access() FROM PUBLIC, mt_app;
                REVOKE ALL ON FUNCTION identity.sync_role_assignment_access() FROM PUBLIC, mt_app;
                REVOKE ALL ON FUNCTION identity.sync_role_name_access() FROM PUBLIC, mt_app;

                -- Repara también accesos previos sin RoleName y conserva reservas todavía sin identidad.
                DO $backfill$
                DECLARE current_tenant_id uuid;
                DECLARE current_member_id uuid;
                DECLARE previous_scope text := current_setting('app.tenant_id', true);
                BEGIN
                    FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" LOOP
                        PERFORM set_config('app.tenant_id', current_tenant_id::text, true);
                        FOR current_member_id IN SELECT "Id" FROM tenant."Members" WHERE "TenantId" = current_tenant_id LOOP
                            PERFORM identity.refresh_user_tenant_access_role(current_tenant_id, current_member_id);
                        END LOOP;
                    END LOOP;
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                END
                $backfill$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION identity.refresh_user_tenant_access_role(p_tenant_id uuid, p_member_id uuid)
                RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                DECLARE current_user_id uuid;
                DECLARE current_role_name text;
                BEGIN
                    SELECT member."UserId" INTO current_user_id
                    FROM tenant."Members" AS member
                    WHERE member."TenantId" = p_tenant_id AND member."Id" = p_member_id;
                    IF NOT FOUND OR current_user_id IS NULL THEN RETURN; END IF;
                    SELECT role."Name" INTO current_role_name
                    FROM tenant."RoleAssignments" AS assignment
                    JOIN tenant."Roles" AS role
                      ON role."TenantId" = assignment."TenantId" AND role."Id" = assignment."RoleId"
                    WHERE assignment."TenantId" = p_tenant_id AND assignment."MemberId" = p_member_id
                      AND assignment."CompanyId" IS NULL AND role."IsSystemRole"
                    ORDER BY CASE WHEN role."Name" = 'TenantAdmin' THEN 0 ELSE 1 END, role."Name"
                    LIMIT 1;
                    UPDATE identity."UserTenantAccesses" SET "RoleName" = current_role_name
                    WHERE "UserId" = current_user_id AND "TenantId" = p_tenant_id;
                END
                $function$;
                CREATE OR REPLACE FUNCTION identity.sync_user_tenant_access() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        DELETE FROM identity."UserTenantAccesses"
                        WHERE "UserId" = OLD."UserId" AND "TenantId" = OLD."TenantId";
                        RETURN OLD;
                    END IF;
                    IF TG_OP = 'UPDATE' AND
                       (OLD."UserId" IS DISTINCT FROM NEW."UserId" OR OLD."TenantId" IS DISTINCT FROM NEW."TenantId") THEN
                        DELETE FROM identity."UserTenantAccesses"
                        WHERE "UserId" = OLD."UserId" AND "TenantId" = OLD."TenantId";
                    END IF;
                    IF NEW."UserId" IS NULL THEN RETURN NEW; END IF;
                    INSERT INTO identity."UserTenantAccesses" ("UserId", "TenantId", "Status", "JoinedAtUtc")
                    VALUES (NEW."UserId", NEW."TenantId", NEW."Status", NEW."JoinedAtUtc")
                    ON CONFLICT ("UserId", "TenantId") DO UPDATE
                    SET "Status" = EXCLUDED."Status", "JoinedAtUtc" = EXCLUDED."JoinedAtUtc";
                    PERFORM identity.refresh_user_tenant_access_role(NEW."TenantId", NEW."Id");
                    RETURN NEW;
                END
                $function$;
                REVOKE ALL ON FUNCTION identity.refresh_user_tenant_access_role(uuid, uuid) FROM PUBLIC, mt_app;
                REVOKE ALL ON FUNCTION identity.sync_user_tenant_access() FROM PUBLIC, mt_app;
                """);
            migrationBuilder.DropTable(
                name: "MemberAccessRoles",
                schema: "identity");
        }
    }
}
