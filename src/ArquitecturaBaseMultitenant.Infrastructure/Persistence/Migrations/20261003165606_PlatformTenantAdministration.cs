using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformTenantAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyCount",
                schema: "platform",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                schema: "platform",
                table: "Tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberCount",
                schema: "platform",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RegisteredByUserId",
                schema: "platform",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationSource",
                schema: "platform",
                table: "Tenants",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "SelfService");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "platform",
                table: "Tenants",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_CreatedAtUtc_Id",
                schema: "platform",
                table: "Tenants",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name_Id",
                schema: "platform",
                table: "Tenants",
                columns: new[] { "Name", "Id" });

            migrationBuilder.Sql("""
                CREATE INDEX "IX_Tenants_Search_Name" ON platform."Tenants"
                    USING gin (public.f_unaccent(lower("Name")) public.gin_trgm_ops);
                CREATE INDEX "IX_Tenants_Search_Slug" ON platform."Tenants"
                    USING gin (public.f_unaccent(lower("Slug")) public.gin_trgm_ops);
                CREATE INDEX "IX_AspNetUsers_Search_DisplayName" ON identity."AspNetUsers"
                    USING gin (public.f_unaccent(lower("DisplayName")) public.gin_trgm_ops);
                CREATE INDEX "IX_AspNetUsers_Search_Email" ON identity."AspNetUsers"
                    USING gin (public.f_unaccent(lower("Email")) public.gin_trgm_ops);

                -- Solo agregados; las filas privadas se escriben con su RLS normal.
                -- Un delta atómico evita perder conteos con dos altas simultáneas.
                CREATE FUNCTION platform.sync_platform_company_count() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        UPDATE platform."Tenants" SET "CompanyCount" = "CompanyCount" + 1 WHERE "Id" = NEW."TenantId";
                        RETURN NEW;
                    ELSE
                        UPDATE platform."Tenants" SET "CompanyCount" = "CompanyCount" - 1 WHERE "Id" = OLD."TenantId";
                        RETURN OLD;
                    END IF;
                END
                $function$;
                REVOKE ALL ON FUNCTION platform.sync_platform_company_count() FROM PUBLIC;
                CREATE TRIGGER sync_platform_company_count AFTER INSERT OR DELETE ON tenant."Companies"
                    FOR EACH ROW EXECUTE FUNCTION platform.sync_platform_company_count();

                CREATE FUNCTION platform.sync_platform_member_count() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                DECLARE change integer;
                DECLARE target_id uuid;
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        change := CASE WHEN NEW."Status" <> 'Removed' THEN 1 ELSE 0 END;
                        target_id := NEW."TenantId";
                    ELSIF TG_OP = 'DELETE' THEN
                        change := CASE WHEN OLD."Status" <> 'Removed' THEN -1 ELSE 0 END;
                        target_id := OLD."TenantId";
                    ELSE
                        change := (CASE WHEN NEW."Status" <> 'Removed' THEN 1 ELSE 0 END) -
                            (CASE WHEN OLD."Status" <> 'Removed' THEN 1 ELSE 0 END);
                        target_id := NEW."TenantId";
                    END IF;
                    IF change <> 0 THEN
                        UPDATE platform."Tenants" SET "MemberCount" = "MemberCount" + change WHERE "Id" = target_id;
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END
                $function$;
                REVOKE ALL ON FUNCTION platform.sync_platform_member_count() FROM PUBLIC;
                CREATE TRIGGER sync_platform_member_count AFTER INSERT OR UPDATE OF "Status" OR DELETE ON tenant."Members"
                    FOR EACH ROW EXECUTE FUNCTION platform.sync_platform_member_count();

                DO $backfill$
                DECLARE current_id uuid;
                BEGIN
                    FOR current_id IN SELECT "Id" FROM platform."Tenants" LOOP
                        PERFORM pg_catalog.set_config('app.tenant_id', current_id::text, true);
                        UPDATE platform."Tenants" SET
                            "CompanyCount" = (SELECT count(*) FROM tenant."Companies" WHERE "TenantId" = current_id),
                            "MemberCount" = (SELECT count(*) FROM tenant."Members" WHERE "TenantId" = current_id AND "Status" <> 'Removed'),
                            "CreatedAtUtc" = (SELECT min("JoinedAtUtc") FROM identity."UserTenantAccesses" WHERE "TenantId" = current_id),
                            "RegisteredByUserId" = (SELECT "UserId" FROM identity."UserTenantAccesses" WHERE "TenantId" = current_id
                                AND "RoleName" = 'TenantAdmin' ORDER BY "JoinedAtUtc", "UserId" LIMIT 1)
                        WHERE "Id" = current_id;
                    END LOOP;
                    PERFORM pg_catalog.set_config('app.tenant_id', '', true);
                END
                $backfill$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER sync_platform_member_count ON tenant."Members";
                DROP TRIGGER sync_platform_company_count ON tenant."Companies";
                DROP FUNCTION platform.sync_platform_member_count();
                DROP FUNCTION platform.sync_platform_company_count();
                DROP INDEX platform."IX_Tenants_Search_Name";
                DROP INDEX platform."IX_Tenants_Search_Slug";
                DROP INDEX identity."IX_AspNetUsers_Search_DisplayName";
                DROP INDEX identity."IX_AspNetUsers_Search_Email";
                """);
            migrationBuilder.DropIndex(
                name: "IX_Tenants_CreatedAtUtc_Id",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Name_Id",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyCount",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MemberCount",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RegisteredByUserId",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RegistrationSource",
                schema: "platform",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "platform",
                table: "Tenants");
        }
    }
}
