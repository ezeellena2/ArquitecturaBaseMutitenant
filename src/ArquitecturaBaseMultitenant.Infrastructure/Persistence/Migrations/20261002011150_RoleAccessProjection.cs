using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleAccessProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.AddColumn<string>(
                name: "RoleName",
                schema: "identity",
                table: "UserTenantAccesses",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedName",
                schema: "tenant",
                table: "Roles",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_NormalizedName",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "NormalizedName" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION identity.refresh_user_tenant_access_role(p_tenant_id uuid, p_member_id uuid)
                RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                DECLARE current_user_id uuid;
                DECLARE current_role_name text;
                BEGIN
                    SELECT member."UserId" INTO current_user_id
                    FROM tenant."Members" AS member
                    WHERE member."TenantId" = p_tenant_id AND member."Id" = p_member_id;
                    IF NOT FOUND OR current_user_id IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT role."Name" INTO current_role_name
                    FROM tenant."RoleAssignments" AS assignment
                    JOIN tenant."Roles" AS role
                      ON role."TenantId" = assignment."TenantId" AND role."Id" = assignment."RoleId"
                    WHERE assignment."TenantId" = p_tenant_id AND assignment."MemberId" = p_member_id
                      AND assignment."CompanyId" IS NULL AND role."IsSystemRole"
                    ORDER BY CASE WHEN role."Name" = 'TenantAdmin' THEN 0 ELSE 1 END, role."Name"
                    LIMIT 1;

                    UPDATE identity."UserTenantAccesses"
                    SET "RoleName" = current_role_name
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

                    IF NEW."UserId" IS NULL THEN
                        RETURN NEW;
                    END IF;

                    INSERT INTO identity."UserTenantAccesses" ("UserId", "TenantId", "Status", "JoinedAtUtc")
                    VALUES (NEW."UserId", NEW."TenantId", NEW."Status", NEW."JoinedAtUtc")
                    ON CONFLICT ("UserId", "TenantId") DO UPDATE
                    SET "Status" = EXCLUDED."Status", "JoinedAtUtc" = EXCLUDED."JoinedAtUtc";
                    PERFORM identity.refresh_user_tenant_access_role(NEW."TenantId", NEW."Id");
                    RETURN NEW;
                END
                $function$;

                CREATE OR REPLACE FUNCTION identity.sync_role_assignment_access() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        PERFORM identity.refresh_user_tenant_access_role(OLD."TenantId", OLD."MemberId");
                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'UPDATE' AND
                       (OLD."TenantId" IS DISTINCT FROM NEW."TenantId" OR OLD."MemberId" IS DISTINCT FROM NEW."MemberId") THEN
                        PERFORM identity.refresh_user_tenant_access_role(OLD."TenantId", OLD."MemberId");
                    END IF;

                    PERFORM identity.refresh_user_tenant_access_role(NEW."TenantId", NEW."MemberId");
                    RETURN NEW;
                END
                $function$;

                CREATE OR REPLACE FUNCTION identity.sync_role_name_access() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                DECLARE affected_member_id uuid;
                BEGIN
                    FOR affected_member_id IN
                        SELECT DISTINCT assignment."MemberId"
                        FROM tenant."RoleAssignments" AS assignment
                        WHERE assignment."TenantId" = NEW."TenantId" AND assignment."RoleId" = NEW."Id"
                    LOOP
                        PERFORM identity.refresh_user_tenant_access_role(NEW."TenantId", affected_member_id);
                    END LOOP;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER sync_role_assignment_access
                AFTER INSERT OR UPDATE OR DELETE ON tenant."RoleAssignments"
                FOR EACH ROW EXECUTE FUNCTION identity.sync_role_assignment_access();

                CREATE TRIGGER sync_role_name_access
                AFTER UPDATE OF "Name", "IsSystemRole" ON tenant."Roles"
                FOR EACH ROW EXECUTE FUNCTION identity.sync_role_name_access();

                REVOKE ALL ON FUNCTION identity.refresh_user_tenant_access_role(uuid, uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION identity.sync_user_tenant_access() FROM PUBLIC;
                REVOKE ALL ON FUNCTION identity.sync_role_assignment_access() FROM PUBLIC;
                REVOKE ALL ON FUNCTION identity.sync_role_name_access() FROM PUBLIC;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS sync_role_assignment_access ON tenant."RoleAssignments";
                DROP TRIGGER IF EXISTS sync_role_name_access ON tenant."Roles";
                DROP FUNCTION IF EXISTS identity.sync_role_assignment_access();
                DROP FUNCTION IF EXISTS identity.sync_role_name_access();
                DROP FUNCTION IF EXISTS identity.refresh_user_tenant_access_role(uuid, uuid);

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

                    IF NEW."UserId" IS NULL THEN
                        RETURN NEW;
                    END IF;

                    INSERT INTO identity."UserTenantAccesses" ("UserId", "TenantId", "Status", "JoinedAtUtc")
                    VALUES (NEW."UserId", NEW."TenantId", NEW."Status", NEW."JoinedAtUtc")
                    ON CONFLICT ("UserId", "TenantId") DO UPDATE
                    SET "Status" = EXCLUDED."Status", "JoinedAtUtc" = EXCLUDED."JoinedAtUtc";
                    RETURN NEW;
                END
                $function$;
                GRANT EXECUTE ON FUNCTION identity.sync_user_tenant_access() TO PUBLIC;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_NormalizedName",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "RoleName",
                schema: "identity",
                table: "UserTenantAccesses");

            migrationBuilder.DropColumn(
                name: "NormalizedName",
                schema: "tenant",
                table: "Roles");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_Name",
                schema: "tenant",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }
    }
}
