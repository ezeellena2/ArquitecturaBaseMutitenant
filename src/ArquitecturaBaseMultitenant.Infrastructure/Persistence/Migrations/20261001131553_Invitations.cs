using System;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Invitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "tenant",
                table: "Members",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateTable(
                name: "Invitations",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Channel = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BootstrapNonceHash = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_InviterUserId",
                        column: x => x.InviterUserId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Members_TenantId_MemberId",
                        columns: x => new { x.TenantId, x.MemberId },
                        principalSchema: "tenant",
                        principalTable: "Members",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedByUserId",
                schema: "tenant",
                table: "Invitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InviterUserId",
                schema: "tenant",
                table: "Invitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TenantId_Destination",
                schema: "tenant",
                table: "Invitations",
                columns: new[] { "TenantId", "Destination" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TenantId_MemberId",
                schema: "tenant",
                table: "Invitations",
                columns: new[] { "TenantId", "MemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_TenantId_TokenHash",
                schema: "tenant",
                table: "Invitations",
                columns: new[] { "TenantId", "TokenHash" },
                unique: true);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "Invitations");
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE ON TABLE tenant."Invitations" TO mt_app;

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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invitations",
                schema: "tenant");

            // El modelo anterior no representa reservas sin identidad. Se retiran bajo su alcance
            // real antes de restaurar NOT NULL; las membresías vinculadas se conservan.
            migrationBuilder.Sql("""
                DO $cleanup$
                DECLARE current_tenant_id uuid;
                BEGIN
                    FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" LOOP
                        PERFORM pg_catalog.set_config('app.tenant_id', current_tenant_id::text, true);
                        DELETE FROM tenant."Members"
                        WHERE "TenantId" = current_tenant_id AND "UserId" IS NULL;
                    END LOOP;
                END
                $cleanup$;

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

                    INSERT INTO identity."UserTenantAccesses" ("UserId", "TenantId", "Status", "JoinedAtUtc")
                    VALUES (NEW."UserId", NEW."TenantId", NEW."Status", NEW."JoinedAtUtc")
                    ON CONFLICT ("UserId", "TenantId") DO UPDATE
                    SET "Status" = EXCLUDED."Status", "JoinedAtUtc" = EXCLUDED."JoinedAtUtc";
                    RETURN NEW;
                END
                $function$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "tenant",
                table: "Members",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
