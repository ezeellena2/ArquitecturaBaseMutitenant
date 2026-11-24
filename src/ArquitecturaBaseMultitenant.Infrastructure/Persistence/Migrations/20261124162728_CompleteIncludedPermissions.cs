using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Un permiso incluye a otro (decisión 260): completa una vez los roles que ya creó cada organización y los permisos
    /// de los operadores con lo que incluyen sus permisos y les falta, así lo guardado es lo que permiten. Lo efectivo
    /// (<c>PermissionInclusions.Effective</c>) queda como red para lo que guarde la versión anterior mientras conviven
    /// las dos (ADR 0060). Excepción de la 0060, como toda migración de esta etapa: se agrega y se escribe en el mismo
    /// despliegue porque todavía no hay despliegue (123). Sin columnas nuevas; el modelo no cambia.
    /// </summary>
    public partial class CompleteIncludedPermissions : Migration
    {
        /// <summary>
        /// Los roles que creó cada organización, una por una con su RLS: cada rol cambiado deja su historia sin autor
        /// (actor de sistema) y la organización avanza la generación de sus roles (<c>t:&lt;organización&gt;:roles</c>,
        /// ADR 0052). Los de sistema no se tocan: ya tienen todo su catálogo, y uno dañado no se repara acá.
        /// Los pares son los de <c>Permissions.Includes</c> el día de la migración, ya en cadena. Lo incluido se calcula
        /// sobre la fila que se actualiza, así un cambio que entra en el medio no se pisa. El arreglo entero queda sin
        /// repetidos y en orden ordinal (<c>COLLATE "C"</c>), el mismo en que lo guarda <c>RoleService</c>: guardar después
        /// el rol sin cambios no deja en su historia un cambio de permisos a nombre de quien lo edita.
        /// </summary>
        internal const string CompleteRolesSql = """
            DO $complete$
            DECLARE current_tenant_id uuid;
            DECLARE completed integer;
            DECLARE previous_scope text := current_setting('app.tenant_id', true);
            BEGIN
                -- mt_owner también respeta RLS forzada; cada actualización usa su alcance privado.
                FOR current_tenant_id IN SELECT "Id" FROM platform."Tenants" WHERE "Kind" = 'Business' ORDER BY "Id" LOOP
                    PERFORM set_config('app.tenant_id', current_tenant_id::text, true);
                    WITH included(code, included_code) AS (
                        VALUES
                            ('users.invite', 'users.read'),
                            ('users.manage', 'users.read'),
                            ('roles.manage', 'roles.read'),
                            ('roles.assign', 'roles.read'),
                            ('companies.manage', 'companies.read'),
                            ('settings.manage', 'settings.read'),
                            ('company.members.manage', 'company.members.read')
                    ), changed AS (
                        UPDATE tenant."Roles" AS role
                        SET "Permissions" = ARRAY(
                            SELECT complete.code
                            FROM (
                                SELECT unnest(role."Permissions") AS code
                                UNION
                                SELECT included.included_code
                                FROM included
                                WHERE included.code = ANY(role."Permissions")
                            ) AS complete
                            ORDER BY complete.code COLLATE "C")
                        WHERE role."TenantId" = current_tenant_id AND NOT role."IsSystemRole"
                            AND EXISTS (
                                SELECT 1 FROM included
                                WHERE included.code = ANY(role."Permissions")
                                    AND NOT (included.included_code = ANY(role."Permissions")))
                        RETURNING role."Id", old."Permissions" AS before, new."Permissions" AS after
                    ), audited AS (
                        INSERT INTO tenant."AuditEntries"
                            ("TenantId", "Id", "ActorKind", "ActorId", "Action", "EntityType", "EntityId", "Changes", "OccurredAtUtc")
                        SELECT current_tenant_id, uuidv7(), 'System', NULL, 'Updated', 'Role', changed."Id",
                            jsonb_build_object('Permissions', jsonb_build_object(
                                'old', to_jsonb(changed.before), 'new', to_jsonb(changed.after))),
                            CURRENT_TIMESTAMP
                        FROM changed
                        RETURNING 1
                    )
                    SELECT count(*) INTO completed FROM audited;
                    IF completed > 0 THEN
                        INSERT INTO platform."CacheTagVersions" ("Tag", "Generation")
                        VALUES ('t:' || replace(current_tenant_id::text, '-', '') || ':roles', 1)
                        ON CONFLICT ("Tag") DO UPDATE SET "Generation" = platform."CacheTagVersions"."Generation" + 1;
                    END IF;
                END LOOP;
                PERFORM set_config('app.tenant_id', coalesce(previous_scope, ''), true);
            END
            $complete$;
            """;

        /// <summary>
        /// Los operadores, con un <c>PlatformOperatorPermissionsChanged</c> de sistema por cada uno que cambia, con lo
        /// agregado. Los pares son los de <c>PlatformPermissions.Includes</c> el día de la migración. El arreglo entero queda
        /// sin repetidos y en el orden de <c>PlatformPermissions.All</c> ese día, el mismo en que lo guarda
        /// <c>PlatformPermissionAssignment</c>: guardar después sin cambios no reescribe los permisos.
        /// </summary>
        internal const string CompleteOperatorsSql = """
            WITH included(code, included_code) AS (
                VALUES
                    ('platform.tenants.manage', 'platform.tenants.read'),
                    ('platform.accounts.manage', 'platform.accounts.read'),
                    ('platform.assistant.configure', 'platform.assistant.read')
            ), catalog(codes) AS (
                SELECT ARRAY[
                    'platform.tenants.read', 'platform.tenants.manage', 'platform.accounts.read',
                    'platform.accounts.manage', 'platform.recoveries.manage', 'platform.legal.manage',
                    'platform.operators.manage', 'platform.audit.read', 'platform.settings.manage',
                    'platform.assistant.use', 'platform.assistant.configure', 'platform.assistant.read',
                    'platform.whatsapp.manage', 'platform.whatsapp.attend']::text[]
            ), changed AS (
                UPDATE platform."PlatformPermissionAssignments" AS assignment
                SET "Permissions" = ARRAY(
                    SELECT complete.code
                    FROM (
                        SELECT unnest(assignment."Permissions") AS code
                        UNION
                        SELECT included.included_code
                        FROM included
                        WHERE included.code = ANY(assignment."Permissions")
                    ) AS complete
                    ORDER BY array_position((SELECT catalog.codes FROM catalog), complete.code) NULLS LAST,
                        complete.code COLLATE "C")
                WHERE EXISTS (
                    SELECT 1 FROM included
                    WHERE included.code = ANY(assignment."Permissions")
                        AND NOT (included.included_code = ANY(assignment."Permissions")))
                RETURNING assignment."UserId", old."Permissions" AS before, new."Permissions" AS after
            )
            INSERT INTO platform."SecurityEvents"
                ("Id", "Type", "ActorKind", "ActorId", "TargetAccountId", "Reason", "OccurredAtUtc", "Changes")
            SELECT uuidv7(), 'PlatformOperatorPermissionsChanged', 'System',
                '00000000-0000-0000-0000-000000000000'::uuid, changed."UserId",
                'Platform operator included permissions', CURRENT_TIMESTAMP,
                (SELECT jsonb_object_agg(permission, jsonb_build_object('before', false, 'after', true))
                    FROM unnest(changed.after) AS permission
                    WHERE NOT (permission = ANY(changed.before)))
            FROM changed;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CompleteRolesSql);
            migrationBuilder.Sql(CompleteOperatorsSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Aditiva: no hay procedencia por permiso para distinguir lo que agregó esta migración de lo que alguien dio
            // a mano. El Down conserva los permisos, la historia y las generaciones, que son append-only.
        }
    }
}
