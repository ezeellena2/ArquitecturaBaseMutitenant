using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SystemRoleFormCatalogPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // E12: el Dueño de cada organización existente recibe los permisos de los catálogos, como los recibe al crearse
            // (SystemRoles le da Permissions.All); CompanyAdmin no. Una instalación que ya tenía uno no lo duplica. mt_owner también
            // respeta RLS forzada: cada actualización usa su alcance privado. Se saltea el guardado, así que avanza a mano la generación de
            // los roles de cada organización que cambia (cache.md §5), como CompleteIncludedPermissions. El SQL va en una línea: no lleva
            // retornos de carro aunque el archivo tenga CRLF.
            migrationBuilder.Sql("""
                DO $upgrade$ DECLARE current_tenant_id uuid; DECLARE changed integer; DECLARE previous_scope text := current_setting('app.tenant_id', true); BEGIN FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" WHERE "Kind" = 'Business' LOOP PERFORM set_config('app.tenant_id', current_tenant_id::text, true); UPDATE tenant."Roles" SET "Permissions" = "Permissions" || CASE WHEN 'formcatalogs.read' = ANY("Permissions") THEN ARRAY[]::text[] ELSE ARRAY['formcatalogs.read'] END || CASE WHEN 'formcatalogs.manage' = ANY("Permissions") THEN ARRAY[]::text[] ELSE ARRAY['formcatalogs.manage'] END WHERE "TenantId" = current_tenant_id AND "IsSystemRole" AND "Name" = 'TenantAdmin' AND "Scope" = 'Organization' AND "CompanyId" IS NULL AND (NOT ('formcatalogs.read' = ANY("Permissions")) OR NOT ('formcatalogs.manage' = ANY("Permissions"))); GET DIAGNOSTICS changed = ROW_COUNT; IF changed > 0 THEN INSERT INTO platform."CacheTagVersions" ("Tag", "Generation") VALUES ('t:' || replace(current_tenant_id::text, '-', '') || ':roles', 1) ON CONFLICT ("Tag") DO UPDATE SET "Generation" = platform."CacheTagVersions"."Generation" + 1; END IF; END LOOP; PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true); END $upgrade$;
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
