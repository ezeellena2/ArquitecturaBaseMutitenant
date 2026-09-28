using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

/// <summary>Plantillas SQL compartidas por migraciones y tablas de aislamiento de pruebas.</summary>
public static class RlsSql
{
    private const string TenantId = "nullif(current_setting('app.tenant_id', true), '')::uuid";

    public static string EnableTenantRls(string schema, string table)
    {
        var target = Table(schema, table);
        var owns = $"\"TenantId\" = {TenantId}";
        return $"""
            ALTER TABLE {target} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {target} FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_scope ON {target} FOR ALL USING ({owns}) WITH CHECK ({owns});
            {PreventTenantChange(schema, table, "TenantId")}
            """;
    }

    public static string EnablePublicRls(string schema, string table)
    {
        var target = Table(schema, table);
        var owns = $"\"BusinessTenantId\" = {TenantId}";
        return $"""
            ALTER TABLE {target} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {target} FORCE ROW LEVEL SECURITY;
            CREATE POLICY public_read ON {target} FOR SELECT USING ("IsPublished" OR {owns});
            CREATE POLICY public_insert ON {target} FOR INSERT WITH CHECK ({owns});
            CREATE POLICY public_update ON {target} FOR UPDATE USING ({owns}) WITH CHECK ({owns});
            CREATE POLICY public_delete ON {target} FOR DELETE USING ({owns});
            {PreventTenantChange(schema, table, "BusinessTenantId")}
            """;
    }

    public static string EnablePartiesRls(string schema, string table)
    {
        var target = Table(schema, table);
        var participant = $"\"ConsumerTenantId\" = {TenantId} OR \"BusinessTenantId\" = {TenantId}";
        return $"""
            ALTER TABLE {target} ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {target} FORCE ROW LEVEL SECURITY;
            CREATE POLICY parties_scope ON {target} FOR ALL USING ({participant}) WITH CHECK ({participant});
            {PreventTenantChange(schema, table, "ConsumerTenantId", "BusinessTenantId")}
            """;
    }

    public static string PreventUpdateDelete(string schema, string table)
    {
        var target = Table(schema, table);
        return $"""
            CREATE TRIGGER prevent_update_delete BEFORE UPDATE OR DELETE ON {target}
            FOR EACH ROW EXECUTE FUNCTION "platform"."prevent_update_delete"();
            """;
    }

    public static string CreateSupportFunctions() => """
        CREATE OR REPLACE FUNCTION "platform"."prevent_tenant_change"() RETURNS trigger
        LANGUAGE plpgsql AS $function$
        DECLARE
            column_name text;
        BEGIN
            FOREACH column_name IN ARRAY TG_ARGV LOOP
                IF (to_jsonb(OLD) ->> column_name) IS DISTINCT FROM (to_jsonb(NEW) ->> column_name) THEN
                    RAISE EXCEPTION 'Tenant columns cannot be changed' USING ERRCODE = '23514';
                END IF;
            END LOOP;
            RETURN NEW;
        END
        $function$;
        CREATE OR REPLACE FUNCTION "platform"."prevent_update_delete"() RETURNS trigger
        LANGUAGE plpgsql AS $function$
        BEGIN
            RAISE EXCEPTION 'Audit history cannot be changed' USING ERRCODE = '23514';
        END
        $function$;
        """;

    private static string PreventTenantChange(string schema, string table, params string[] columns)
    {
        var arguments = string.Join(", ", columns.Select(column => $"'{Identifier(column)}'"));
        return $"""
            CREATE TRIGGER prevent_tenant_change BEFORE UPDATE ON {Table(schema, table)}
            FOR EACH ROW EXECUTE FUNCTION "platform"."prevent_tenant_change"({arguments});
            """;
    }

    private static string Table(string schema, string table) => $"{Identifier(schema)}.{Identifier(table)}";

    private static string Identifier(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 63 ||
            !Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("SQL identifier is invalid.", nameof(value));
        }

        return $"\"{value}\"";
    }
}
