using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserTenantAccessIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTenantAccesses",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenantAccesses", x => new { x.UserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_UserTenantAccesses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenantAccesses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenantAccesses_TenantId",
                schema: "identity",
                table: "UserTenantAccesses",
                column: "TenantId");

            // El runtime solo consulta el índice; el trigger, propiedad de mt_owner, lo mantiene en la
            // misma transacción del cambio de Members sin ampliar los privilegios de mt_app.
            migrationBuilder.Sql("""
                REVOKE ALL PRIVILEGES ON TABLE identity."UserTenantAccesses" FROM mt_app;
                GRANT SELECT ON TABLE identity."UserTenantAccesses" TO mt_app;

                CREATE FUNCTION identity.sync_user_tenant_access() RETURNS trigger
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

                    INSERT INTO identity."UserTenantAccesses" ("UserId", "TenantId", "Status", "JoinedAtUtc")
                    VALUES (NEW."UserId", NEW."TenantId", NEW."Status", NEW."JoinedAtUtc")
                    ON CONFLICT ("UserId", "TenantId") DO UPDATE
                    SET "Status" = EXCLUDED."Status", "JoinedAtUtc" = EXCLUDED."JoinedAtUtc";
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER sync_user_tenant_access
                AFTER INSERT OR UPDATE OR DELETE ON tenant."Members"
                FOR EACH ROW EXECUTE FUNCTION identity.sync_user_tenant_access();

                -- Existing tenant memberships are copied using their own RLS scope, one tenant at a time.
                DO $backfill$
                DECLARE current_tenant_id uuid;
                BEGIN
                    FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" LOOP
                        PERFORM pg_catalog.set_config('app.tenant_id', current_tenant_id::text, true);
                        INSERT INTO identity."UserTenantAccesses" ("UserId", "TenantId", "Status", "JoinedAtUtc")
                        SELECT "UserId", "TenantId", "Status", "JoinedAtUtc"
                        FROM tenant."Members" WHERE "TenantId" = current_tenant_id
                        ON CONFLICT ("UserId", "TenantId") DO UPDATE
                        SET "Status" = EXCLUDED."Status", "JoinedAtUtc" = EXCLUDED."JoinedAtUtc";
                    END LOOP;
                END
                $backfill$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER sync_user_tenant_access ON tenant."Members";
                DROP FUNCTION identity.sync_user_tenant_access();
                REVOKE ALL PRIVILEGES ON TABLE identity."UserTenantAccesses" FROM mt_app;
                """);
            migrationBuilder.DropTable(
                name: "UserTenantAccesses",
                schema: "identity");
        }
    }
}
