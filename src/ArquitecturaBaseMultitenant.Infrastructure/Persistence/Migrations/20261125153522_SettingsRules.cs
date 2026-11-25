using System;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// B18 (decisiones 270, 272 y 273): <c>platform.PlatformSettings</c> suma los tres plazos del núcleo, con su valor por
    /// defecto en la base para que el alta de una versión anterior lo tome, y el nombre, el remitente y el logo de los correos,
    /// nulos hasta que Plataforma los guarda (valen <c>Email:AppName</c> y <c>Email:Smtp:FromName</c>). Nacen
    /// <c>platform.WhatsAppPlatformSettings</c> (global, una fila) y <c>tenant.WhatsAppOrganizationSettings</c> (privada, con RLS);
    /// sin fila valen los valores por defecto. Aditiva; se escribe en el mismo despliegue que la lee, como excepción anotada de la
    /// 0060 en esta etapa (todavía no hay despliegue, 123): una versión anterior no conoce las columnas ni las tablas nuevas.
    /// Un logo que Plataforma quitó queda vacío, no nulo, para que no vuelva el de arranque. El Down se niega si hay una regla guardada.
    /// </summary>
    public partial class SettingsRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppName",
                schema: "platform",
                table: "PlatformSettings",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClosedInvitationRetentionDays",
                schema: "platform",
                table: "PlatformSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "EmailLogoUrl",
                schema: "platform",
                table: "PlatformSettings",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoginAuditRetentionMonths",
                schema: "platform",
                table: "PlatformSettings",
                type: "integer",
                nullable: false,
                defaultValue: 12);

            migrationBuilder.AddColumn<int>(
                name: "LoginCodeRetentionDays",
                schema: "platform",
                table: "PlatformSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "SenderName",
                schema: "platform",
                table: "PlatformSettings",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppOrganizationSettings",
                schema: "tenant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChatRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppOrganizationSettings", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_WhatsAppOrganizationSettings_ChatRetentionDays", "\"ChatRetentionDays\" BETWEEN 1 AND 3650");
                    table.ForeignKey(
                        name: "FK_WhatsAppOrganizationSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "platform",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppPlatformSettings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowedCountries = table.Column<string>(type: "jsonb", nullable: false),
                    ChatRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    MaxOrganizationChatRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppPlatformSettings", x => x.Id);
                    table.CheckConstraint("CK_WhatsAppPlatformSettings_ChatRetentionDays", "\"ChatRetentionDays\" BETWEEN 1 AND 3650");
                    table.CheckConstraint("CK_WhatsAppPlatformSettings_MaxOrganizationChatRetentionDays", "\"MaxOrganizationChatRetentionDays\" BETWEEN 1 AND 3650");
                    table.CheckConstraint("CK_WhatsAppPlatformSettings_Singleton", "\"Id\" = '00000000-0000-0000-0000-00000000a9a2'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOrganizationSettings_TenantId",
                schema: "tenant",
                table: "WhatsAppOrganizationSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.EnableTenantRls(Schemas.Tenant, "WhatsAppOrganizationSettings");
            // Se leen y se guardan; nunca se borran (una organización que se va las pierde por su llave).
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE ON TABLE
                tenant."WhatsAppOrganizationSettings",
                platform."WhatsAppPlatformSettings"
                TO mt_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Se niega antes de cualquier DDL si Plataforma o una organización ya guardaron una regla: la versión anterior volvería a
            // los plazos fijos y a appsettings sin avisar. Unos plazos iguales a los de siempre y nada más no pierden nada. Los locks
            // van en el orden de los Drop; la tabla de la organización fuerza la RLS también para su dueño, que corre la migración
            // sin organización: sin NO FORCE la guarda no vería sus filas. El SQL va en una línea: no lleva \r aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                LOCK TABLE tenant."WhatsAppOrganizationSettings", platform."WhatsAppPlatformSettings", platform."PlatformSettings" IN ACCESS EXCLUSIVE MODE; ALTER TABLE tenant."WhatsAppOrganizationSettings" NO FORCE ROW LEVEL SECURITY; DO $$ BEGIN IF EXISTS (SELECT 1 FROM tenant."WhatsAppOrganizationSettings") OR EXISTS (SELECT 1 FROM platform."WhatsAppPlatformSettings") OR EXISTS (SELECT 1 FROM platform."PlatformSettings" WHERE "LoginAuditRetentionMonths" <> 12 OR "LoginCodeRetentionDays" <> 30 OR "ClosedInvitationRetentionDays" <> 30 OR "AppName" IS NOT NULL OR "SenderName" IS NOT NULL OR "EmailLogoUrl" IS NOT NULL) THEN RAISE EXCEPTION 'Rules saved in settings cannot be represented by the previous schema.'; END IF; END $$; ALTER TABLE tenant."WhatsAppOrganizationSettings" FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "WhatsAppOrganizationSettings",
                schema: "tenant");

            migrationBuilder.DropTable(
                name: "WhatsAppPlatformSettings",
                schema: "platform");

            migrationBuilder.DropColumn(
                name: "AppName",
                schema: "platform",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "ClosedInvitationRetentionDays",
                schema: "platform",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "EmailLogoUrl",
                schema: "platform",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "LoginAuditRetentionMonths",
                schema: "platform",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "LoginCodeRetentionDays",
                schema: "platform",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "SenderName",
                schema: "platform",
                table: "PlatformSettings");
        }
    }
}
