using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Reemplaza los roles Owner/Support de los operadores por permisos individuales. Conserva cada fila, su xmin,
    /// la FK y los permisos del runtime; cada rol se convierte en los permisos que concedía.
    /// </summary>
    public partial class PlatformOperatorPermissions : Migration
    {
        private const string AllPermissions = """
            ARRAY['platform.tenants.read', 'platform.tenants.manage', 'platform.accounts.read',
                'platform.accounts.manage', 'platform.recoveries.manage', 'platform.legal.manage',
                'platform.operators.manage', 'platform.audit.read', 'platform.settings.manage']::text[]
            """;

        private const string SupportPermissions = """
            ARRAY['platform.tenants.read', 'platform.accounts.read', 'platform.recoveries.manage',
                'platform.audit.read']::text[]
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PlatformRoleAssignments",
                schema: "platform",
                newName: "PlatformPermissionAssignments",
                newSchema: "platform");

            // Un rol desconocido deja Permissions en null y la migración falla en SET NOT NULL, sin inventar permisos.
            migrationBuilder.Sql($"""
                ALTER TABLE platform."PlatformPermissionAssignments"
                    RENAME CONSTRAINT "PK_PlatformRoleAssignments" TO "PK_PlatformPermissionAssignments";
                ALTER TABLE platform."PlatformPermissionAssignments"
                    RENAME CONSTRAINT "FK_PlatformRoleAssignments_AspNetUsers_UserId"
                    TO "FK_PlatformPermissionAssignments_AspNetUsers_UserId";
                ALTER INDEX platform."IX_PlatformRoleAssignments_UserId"
                    RENAME TO "IX_PlatformPermissionAssignments_UserId";

                ALTER TABLE platform."PlatformPermissionAssignments" ADD COLUMN "Permissions" text[];
                UPDATE platform."PlatformPermissionAssignments" SET "Permissions" = CASE "Role"
                    WHEN 'Owner' THEN {AllPermissions}
                    WHEN 'Support' THEN {SupportPermissions}
                END;
                ALTER TABLE platform."PlatformPermissionAssignments" ALTER COLUMN "Permissions" SET NOT NULL;
                ALTER TABLE platform."PlatformPermissionAssignments" DROP COLUMN "Role";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Con pérdida: solo el catálogo completo vuelve a Owner; cualquier otra combinación pasa a Support.
            migrationBuilder.Sql($"""
                ALTER TABLE platform."PlatformPermissionAssignments" ADD COLUMN "Role" character varying(60);
                UPDATE platform."PlatformPermissionAssignments" SET "Role" = CASE
                    WHEN "Permissions" @> {AllPermissions} THEN 'Owner'
                    ELSE 'Support'
                END;
                ALTER TABLE platform."PlatformPermissionAssignments" ALTER COLUMN "Role" SET NOT NULL;
                ALTER TABLE platform."PlatformPermissionAssignments" DROP COLUMN "Permissions";

                ALTER INDEX platform."IX_PlatformPermissionAssignments_UserId"
                    RENAME TO "IX_PlatformRoleAssignments_UserId";
                ALTER TABLE platform."PlatformPermissionAssignments"
                    RENAME CONSTRAINT "FK_PlatformPermissionAssignments_AspNetUsers_UserId"
                    TO "FK_PlatformRoleAssignments_AspNetUsers_UserId";
                ALTER TABLE platform."PlatformPermissionAssignments"
                    RENAME CONSTRAINT "PK_PlatformPermissionAssignments" TO "PK_PlatformRoleAssignments";
                """);

            migrationBuilder.RenameTable(
                name: "PlatformPermissionAssignments",
                schema: "platform",
                newName: "PlatformRoleAssignments",
                newSchema: "platform");
        }
    }
}
