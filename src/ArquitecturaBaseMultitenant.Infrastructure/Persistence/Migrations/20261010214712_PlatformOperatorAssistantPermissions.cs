using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformOperatorAssistantPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Upgrade puntual de asignaciones existentes, antes del tráfico; no es un seed recurrente.
            // Se conserva el array anterior tal cual, incluidos orden y duplicados ajenos al módulo.
            migrationBuilder.Sql("""
                WITH candidates AS (
                    SELECT assignment."Id", ARRAY(
                        SELECT permission.code
                        FROM (VALUES
                            ('platform.assistant.use', 1),
                            ('platform.assistant.configure', 2),
                            ('platform.assistant.read', 3)
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
                    'Platform operator assistant permissions upgrade', CURRENT_TIMESTAMP,
                    (SELECT jsonb_object_agg(permission, jsonb_build_object('before', false, 'after', true))
                        FROM unnest(changed.missing) AS permission)
                FROM changed;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se puede distinguir un grant previo/manual de uno agregado por este upgrade.
            // Conservar permisos y evidencia append-only; el retiro del módulo tiene su propio preflight.
        }
    }
}
