using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

/// <summary>
/// La base no tiene privilegios por defecto: cada migración que crea una tabla le da su <c>GRANT</c> a <c>mt_app</c>, el rol con
/// el que corre la Api. Si falta, la Api responde 500 con «permission denied» (42501) en cuanto lee la tabla, como pasó con
/// <c>tenant."AssistantProposals"</c>. Este test recorre todas las tablas que mapea el modelo, ya migradas, y pide al menos
/// <c>SELECT</c>: algunas son de solo lectura para la Api, porque las escriben funciones <c>SECURITY DEFINER</c>.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RuntimeRoleTableGrantsTests(ApiFactory factory)
{
    /// <summary>Tablas que la Api nunca toca: solo las leen y escriben funciones <c>SECURITY DEFINER</c>.</summary>
    private static readonly string[] OnlyThroughFunctions =
    [
        // La proyección del rol de acceso de cada miembro (20261007135517_MemberAccessRoleProjection), con REVOKE ALL a mt_app.
        "identity.MemberAccessRoles",
    ];

    [Fact]
    public async Task Runtime_role_can_read_every_table_the_model_maps()
    {
        var ct = TestContext.Current.CancellationToken;
        string[] tables;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            tables = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Model.GetEntityTypes()
                .Where(entity => entity.GetTableName() is not null && entity.GetViewName() is null && entity.GetSqlQuery() is null)
                .Select(entity => $"{entity.GetSchema() ?? "public"}.{entity.GetTableName()}")
                .Except(OnlyThroughFunctions, StringComparer.Ordinal)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        }
        Assert.NotEmpty(tables);

        await using var admin = new NpgsqlConnection(factory.AdminConnectionString);
        await admin.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT name
            FROM unnest(@tables) AS name
            WHERE to_regclass(format('%I.%I', split_part(name, '.', 1), split_part(name, '.', 2))) IS NULL
               OR NOT has_table_privilege('mt_app', format('%I.%I', split_part(name, '.', 1), split_part(name, '.', 2)), 'SELECT')
            ORDER BY name
            """, admin);
        command.Parameters.AddWithValue("tables", tables);
        var missing = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct)) missing.Add(reader.GetString(0));
        }

        Assert.True(missing.Count == 0, $"Sin tabla o sin GRANT SELECT a mt_app: {string.Join(", ", missing)}");
    }
}
