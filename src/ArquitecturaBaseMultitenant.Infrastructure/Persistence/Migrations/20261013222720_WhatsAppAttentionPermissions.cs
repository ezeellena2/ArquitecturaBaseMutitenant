using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppAttentionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // D3 (2026-10-13): solo el Dueño recibe de entrada los cuatro permisos de WhatsApp; el Administrador de empresa no.
            migrationBuilder.Sql("""
                DO $upgrade$
                DECLARE current_tenant_id uuid;
                DECLARE previous_scope text := current_setting('app.tenant_id', true);
                BEGIN
                    -- mt_owner también respeta RLS forzada; cada actualización usa su alcance privado.
                    FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" WHERE "Kind" = 'Business' LOOP
                        PERFORM set_config('app.tenant_id', current_tenant_id::text, true);
                        UPDATE tenant."Roles" AS role
                        SET "Permissions" = role."Permissions" || ARRAY(
                            SELECT permission.code
                            FROM (VALUES
                                ('whatsapp.customers', 1),
                                ('whatsapp.internal', 2),
                                ('whatsapp.broadcasts', 3),
                                ('whatsapp.manage', 4)
                            ) AS permission(code, ordinal)
                            WHERE NOT (permission.code = ANY(role."Permissions"))
                            ORDER BY permission.ordinal)
                        WHERE role."TenantId" = current_tenant_id AND role."IsSystemRole"
                            AND role."Name" = 'TenantAdmin' AND role."Scope" = 'Organization' AND role."CompanyId" IS NULL
                            AND NOT (ARRAY['whatsapp.customers', 'whatsapp.internal', 'whatsapp.broadcasts', 'whatsapp.manage'] <@ role."Permissions");
                    END LOOP;
                    PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
                END
                $upgrade$;
                """);

            // Upgrade puntual de operadores existentes, antes del tráfico; no es un seed recurrente.
            migrationBuilder.Sql("""
                WITH candidates AS (
                    SELECT assignment."Id", ARRAY(
                        SELECT permission.code
                        FROM (VALUES
                            ('platform.whatsapp.manage', 1),
                            ('platform.whatsapp.attend', 2)
                        ) AS permission(code, ordinal)
                        WHERE NOT (permission.code = ANY(assignment."Permissions"))
                        ORDER BY permission.ordinal
                    ) AS missing
                    FROM platform."PlatformPermissionAssignments" AS assignment
                    WHERE 'platform.operators.manage' = ANY(assignment."Permissions")
                ), changed AS (
                    UPDATE platform."PlatformPermissionAssignments" AS assignment
                    SET "Permissions" = assignment."Permissions" || candidates.missing
                    FROM candidates
                    WHERE assignment."Id" = candidates."Id" AND cardinality(candidates.missing) > 0
                    RETURNING assignment."UserId", candidates.missing
                )
                INSERT INTO platform."SecurityEvents"
                    ("Id", "Type", "ActorKind", "ActorId", "TargetAccountId", "Reason", "OccurredAtUtc", "Changes")
                SELECT uuidv7(), 'PlatformOperatorPermissionsChanged', 'System',
                    '00000000-0000-0000-0000-000000000000'::uuid, changed."UserId",
                    'Platform operator WhatsApp permissions upgrade', CURRENT_TIMESTAMP,
                    (SELECT jsonb_object_agg(permission, jsonb_build_object('before', false, 'after', true))
                        FROM unnest(changed.missing) AS permission)
                FROM changed;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Upgrade aditivo: no hay procedencia por permiso para distinguir un grant manual de este.
            // Down conserva permisos y evidencia append-only; el retiro del módulo tiene su propia normalización.
        }
    }
}
