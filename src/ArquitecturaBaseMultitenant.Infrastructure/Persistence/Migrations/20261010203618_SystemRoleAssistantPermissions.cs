using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemRoleAssistantPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $upgrade$
                DECLARE current_tenant_id uuid;
                DECLARE previous_scope text := current_setting('app.tenant_id', true);
                BEGIN
                    -- mt_owner también respeta RLS forzada; cada actualización usa su alcance privado.
                    FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" WHERE "Kind" = 'Business' LOOP
                        PERFORM set_config('app.tenant_id', current_tenant_id::text, true);
                        UPDATE tenant."Roles"
                        SET "Permissions" = "Permissions"
                            || CASE WHEN 'assistant.use' = ANY("Permissions") THEN ARRAY[]::text[] ELSE ARRAY['assistant.use'] END
                            || CASE WHEN 'assistant.configure' = ANY("Permissions") THEN ARRAY[]::text[] ELSE ARRAY['assistant.configure'] END
                        WHERE "TenantId" = current_tenant_id AND "IsSystemRole"
                            AND "Name" = 'TenantAdmin' AND "Scope" = 'Organization' AND "CompanyId" IS NULL
                            AND (NOT ('assistant.use' = ANY("Permissions")) OR NOT ('assistant.configure' = ANY("Permissions")));

                        UPDATE tenant."Roles"
                        SET "Permissions" = array_append("Permissions", 'assistant.use')
                        WHERE "TenantId" = current_tenant_id AND "IsSystemRole"
                            AND "Name" = 'CompanyAdmin' AND "Scope" = 'AnyCompany' AND "CompanyId" IS NULL
                            AND NOT ('assistant.use' = ANY("Permissions"));
                    END LOOP;
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                END
                $upgrade$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Upgrade aditivo: no hay procedencia por permiso para distinguir los ya existentes.
            // Down conserva los datos; retirar el módulo requiere su propia migración de normalización.
        }
    }
}
