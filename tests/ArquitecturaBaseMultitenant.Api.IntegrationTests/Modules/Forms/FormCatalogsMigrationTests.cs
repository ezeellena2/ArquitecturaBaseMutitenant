using UniqueKeys = ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence.UniqueKeys;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Forms;

/// <summary>
/// La migración de los catálogos de ida y vuelta (guía de migración, paso 4): con un catálogo guardado el Down se niega antes de
/// cualquier DDL y con la RLS forzada; sin catálogos vuelve atrás, aunque haya una barrera de archivos, que vence sola, y vuelve a
/// subir con sus restricciones diferidas, su RLS y sus índices. La guarda corre como la migración: el dueño, sin organización.
/// </summary>
public sealed class FormCatalogsMigrationTests
{
    private const string Before = "20261127120737_SystemRoleFormCatalogPermissions";
    private const string Catalogs = "20261127123346_FormCatalogs";

    [Fact]
    public async Task Down_refuses_to_lose_a_catalog_and_goes_back_and_up_without_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Migración de catálogos " + Guid.NewGuid().ToString("N")[..8], ct);
        var catalogId = await CatalogWithAnOptionAsync(isolated, tenantId, ct);
        await InTenantAsync(isolated, tenantId, """INSERT INTO tenant."FormWorkLeases" ("Id", "TenantId", "UserId", "Kind", "ExpiresAtUtc") VALUES (gen_random_uuid(), @tenant, @id, 'Export', now()) RETURNING 1""", owner.UserId, ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();

        // Con un catálogo guardado, el Down se niega: las tablas, el catálogo y su opción quedan, con la RLS forzada.
        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(Before, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains(Catalogs, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal(1L, await InTenantAsync(isolated, tenantId, """SELECT count(*) FROM tenant."FormCatalogItems" WHERE "CatalogId" = @id""", catalogId, ct));
        Assert.True(await RlsForcedAsync(isolated, "FormCatalogs", ct));

        Assert.Equal(1L, await InTenantAsync(isolated, tenantId, """WITH gone AS (DELETE FROM tenant."FormCatalogs" WHERE "Id" = @id RETURNING 1) SELECT count(*) FROM gone""", catalogId, ct));
        await migrator.MigrateAsync(Before, ct);
        Assert.DoesNotContain(Catalogs, await db.Database.GetAppliedMigrationsAsync(ct));

        await migrator.MigrateAsync(Catalogs, ct);
        Assert.Contains(Catalogs, await db.Database.GetAppliedMigrationsAsync(ct));
        foreach (var table in new[] { "FormCatalogs", "FormCatalogItems", "FormWorkLeases" })
            Assert.True(await RlsForcedAsync(isolated, table, ct), table);
        await using (var connection = new NpgsqlConnection(isolated.AdminConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var deferred = new NpgsqlCommand("""
                SELECT count(*) FROM pg_constraint WHERE conrelid = 'tenant."FormCatalogItems"'::regclass AND condeferrable AND condeferred AND conname = ANY(@names)
                """, connection);
            deferred.Parameters.AddWithValue("names", new[] { UniqueKeys.FormCatalogItemCode, UniqueKeys.FormCatalogItemPosition });
            Assert.Equal(2L, await deferred.ExecuteScalarAsync(ct));
        }
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static async Task<Guid> CatalogWithAnOptionAsync(ApiFactory factory, Guid tenantId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var catalog = FormCatalog.CreateManual("Talles", null, FormCatalogValueType.Text);
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var repository = services.GetRequiredService<IFormCatalogRepository>();
            repository.Add(catalog);
            repository.Add(FormCatalogItem.Create(catalog, 0, "S", "Chico", null, null, true, 0).Value);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, ct);
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
        return catalog.Id;
    }

    private static async Task<bool> RlsForcedAsync(ApiFactory factory, string table, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"{table}\"'::regclass", connection);
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    // El dueño también pasa por la RLS forzada: para ver o tocar una fila entra a su organización, como la app.
    private static async Task<long> InTenantAsync(ApiFactory factory, Guid tenantId, string sql, Guid id, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);
        await using (var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection))
        {
            enter.Parameters.AddWithValue("tenant", tenantId.ToString());
            await enter.ExecuteNonQueryAsync(ct);
        }
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("tenant", tenantId);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
