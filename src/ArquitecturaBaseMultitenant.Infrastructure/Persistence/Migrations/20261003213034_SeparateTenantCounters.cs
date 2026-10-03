using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateTenantCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantCounters",
                schema: "platform",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MemberCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantCounters", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantCounters_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Conserva los agregados sin releer filas privadas ni actualizar la fila versionada.
            migrationBuilder.Sql("""
                INSERT INTO platform."TenantCounters" ("TenantId", "CompanyCount", "MemberCount")
                SELECT "Id", "CompanyCount", "MemberCount" FROM platform."Tenants";

                CREATE FUNCTION platform.create_tenant_counters() RETURNS trigger
                LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
                BEGIN
                    INSERT INTO platform."TenantCounters" ("TenantId") VALUES (NEW."Id");
                    RETURN NEW;
                END
                $function$;
                REVOKE ALL ON FUNCTION platform.create_tenant_counters() FROM PUBLIC;
                CREATE TRIGGER create_tenant_counters AFTER INSERT ON platform."Tenants"
                    FOR EACH ROW EXECUTE FUNCTION platform.create_tenant_counters();
                GRANT SELECT ON platform."TenantCounters" TO mt_app;
                """);
            migrationBuilder.Sql(CounterFunctions("TenantCounters", "TenantId"));
            migrationBuilder.DropColumn(name: "CompanyCount", schema: "platform", table: "Tenants");
            migrationBuilder.DropColumn(name: "MemberCount", schema: "platform", table: "Tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CompanyCount",
                schema: "platform",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MemberCount",
                schema: "platform",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE platform."Tenants" AS t SET "CompanyCount" = c."CompanyCount", "MemberCount" = c."MemberCount"
                FROM platform."TenantCounters" AS c WHERE t."Id" = c."TenantId";
                DROP TRIGGER create_tenant_counters ON platform."Tenants";
                DROP FUNCTION platform.create_tenant_counters();
                """);
            migrationBuilder.Sql(CounterFunctions("Tenants", "Id"));
            migrationBuilder.DropTable(name: "TenantCounters", schema: "platform");
        }

        // Los constraint triggers existentes conservan su ejecución diferida y deltas atómicos.
        private static string CounterFunctions(string table, string key) => $"""
            CREATE OR REPLACE FUNCTION platform.sync_platform_company_count() RETURNS trigger
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $function$
            BEGIN
                IF TG_OP = 'INSERT' THEN
                    UPDATE platform."{table}" SET "CompanyCount" = "CompanyCount" + 1 WHERE "{key}" = NEW."TenantId";
                    RETURN NEW;
                ELSE
                    UPDATE platform."{table}" SET "CompanyCount" = "CompanyCount" - 1 WHERE "{key}" = OLD."TenantId";
                    RETURN OLD;
                END IF;
            END
            $function$;
            REVOKE ALL ON FUNCTION platform.sync_platform_company_count() FROM PUBLIC;

            CREATE OR REPLACE FUNCTION platform.sync_platform_member_count() RETURNS trigger
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
                    UPDATE platform."{table}" SET "MemberCount" = "MemberCount" + change WHERE "{key}" = target_id;
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END
            $function$;
            REVOKE ALL ON FUNCTION platform.sync_platform_member_count() FROM PUBLIC;
            """;
    }
}
